using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Fenrir.Core.Services;

public class RecognitionService
{
    private readonly string _pythonScriptPath;

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
            return text;
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
        // Используем WaveInEvent — он точно есть в NAudio для .NET 8
        var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1)
        };

        var writer = new WaveFileWriter(outputPath, waveIn.WaveFormat);

        var tcs = new TaskCompletionSource<bool>();

        waveIn.DataAvailable += (sender, args) =>
        {
            if (args.BytesRecorded > 0)
                writer.Write(args.Buffer, 0, args.BytesRecorded);
        };

        waveIn.RecordingStopped += (sender, args) =>
        {
            writer.Dispose();
            waveIn.Dispose();
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

        waveIn.StopRecording();
        await tcs.Task; // Ждём завершения остановки
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