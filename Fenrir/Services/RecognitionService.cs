using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Fenrir.Core.Services;

public class RecognitionService
{
    private readonly string _pythonScriptPath;

    private bool _isRecording = false;
    private readonly object _recordLock = new object();

    public RecognitionService(string pythonScriptPath)
    {
        _pythonScriptPath = pythonScriptPath;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private bool IsKeyPressed(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// Ждёт нажатия Ctrl+Shift, записывает аудио с микрофона,
    /// отправляет в Python Whisper, возвращает распознанный текст.
    /// </summary>
    public async Task<string> ListenAsync(CancellationToken cancellationToken = default)
    {
        string tempWavPath = Path.Combine(Path.GetTempPath(), $"fenrir_{Guid.NewGuid()}.wav");

        try
        {
            Console.WriteLine("⏳ Зажмите Ctrl+Shift и говорите...");

            // Ждём нажатия обеих клавиш
            while (!(IsKeyPressed(0x11) && IsKeyPressed(0x10)))
            {
                if (cancellationToken.IsCancellationRequested)
                    return string.Empty;
                await Task.Delay(100, cancellationToken);
            }

            Console.WriteLine("🔴 Запись...");

            // Записываем аудио
            await RecordAudio(tempWavPath, cancellationToken);

            Console.WriteLine("⏹ Распознаю...");

            // Отдаём Python Whisper
            string text = await TranscribeAsync(tempWavPath, cancellationToken);
            // Игнорируем результат, если это известная галлюцинация или слишком короткая запись
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Игнорируем известные галлюцинации Whisper
            if (text.Contains("Редактор субтитров", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Закомолдина", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("ПОЗИТИВАЮЩАЯ МУЗЫКА", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("субтитров", StringComparison.OrdinalIgnoreCase))                
            {
                return string.Empty;
            }

            return text;
        }
        catch (OperationCanceledException)
        {
            // Нормально: клавиатурный ввод отменил голос
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка STT: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            if (File.Exists(tempWavPath))
                File.Delete(tempWavPath);
        }
    }

    private async Task RecordAudio(string outputPath, CancellationToken ct)
    {
        // Защита от повторного входа
        if (_isRecording)
            return;

        lock (_recordLock)
        {
            if (_isRecording)
                return;
            _isRecording = true;
        }

        try
        {
            var waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50  // Меньше задержка
            };

            var writer = new WaveFileWriter(outputPath, waveIn.WaveFormat);
            var tcs = new TaskCompletionSource<bool>();

            waveIn.DataAvailable += (sender, args) =>
            {
                if (args.BytesRecorded > 0)
                {
                    try
                    {
                        writer.Write(args.Buffer, 0, args.BytesRecorded);
                    }
                    catch (ObjectDisposedException) { }
                }
            };

            waveIn.RecordingStopped += (sender, args) =>
            {
                try
                {
                    writer.Dispose();
                    waveIn.Dispose();
                }
                catch { }
                tcs.TrySetResult(true);
            };

            waveIn.StartRecording();

            // Ждём отпускания клавиш
            while (IsKeyPressed(0x11) && IsKeyPressed(0x10))
            {
                if (ct.IsCancellationRequested)
                    break;
                await Task.Delay(100, ct);
            }

            // Останавливаем запись
            try
            {
                waveIn.StopRecording();
                // Ждём завершения с таймаутом
                await Task.WhenAny(tcs.Task, Task.Delay(2000));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка при остановке записи: {ex.Message}");
            }
        }
        finally
        {
            lock (_recordLock)
            {
                _isRecording = false;
            }
        }
    }

    private async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{_pythonScriptPath}\" \"{wavPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process == null) return string.Empty;

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        string errors = await errorTask;
        if (!string.IsNullOrEmpty(errors) && !errors.Contains("symlinks", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"🐍 {errors}");

        return (await outputTask).Trim();
    }
}