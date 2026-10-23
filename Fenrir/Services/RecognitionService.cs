using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Fenrir.Core.Services;

public class RecognitionService
{
    private readonly string _pythonScriptPath;
    private readonly string _pythonExe;

    private bool _isRecording = false;
    private readonly object _recordLock = new object();

    public RecognitionService(string pythonScriptPath)
    {
        _pythonScriptPath = pythonScriptPath;
        _pythonExe = ResolvePythonExecutable();
    }

    internal static string ResolvePythonExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("FENRIR_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);

        string? localVenv = FindLocalVenvPython();
        if (localVenv != null)
            return localVenv;

        string appPython = Path.Combine(AppContext.BaseDirectory, "python.exe");
        if (File.Exists(appPython))
            return appPython;

        string cwdPython = Path.Combine(Environment.CurrentDirectory, "python.exe");
        if (File.Exists(cwdPython))
            return cwdPython;

        return FindOnPath("python.exe") ?? "python";
    }

    private static string? FindLocalVenvPython()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        for (int depth = 0; depth < 6 && current != null; depth++, current = current.Parent)
        {
            string candidate = Path.Combine(current.FullName, "python_stt", ".venv", "Scripts", "python.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (string entry in path.Split(Path.PathSeparator))
        {
            string directory = entry.Trim().Trim('"');
            if (directory.Length == 0)
                continue;

            try
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return null;
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
            if (!await RecordAudio(tempWavPath, cancellationToken))
                return string.Empty;

            // Клавиатурный ввод отменил голосовой — не тратим время на распознавание
            if (cancellationToken.IsCancellationRequested)
                return string.Empty;

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
            TryDeleteFile(tempWavPath);
        }
    }

    internal static void TryDeleteFile(string path)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private async Task<bool> RecordAudio(string outputPath, CancellationToken ct)
    {
        // Защита от повторного входа
        if (_isRecording)
            return false;

        lock (_recordLock)
        {
            if (_isRecording)
                return false;
            _isRecording = true;
        }

        WaveInEvent? waveIn = null;
        WaveFileWriter? writer = null;
        var writerLock = new object();
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool writerClosed = false;
        bool recordingStarted = false;
        Exception? captureError = null;

        try
        {
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50
            };

            var fileWriter = new WaveFileWriter(outputPath, waveIn.WaveFormat);
            writer = fileWriter;

            waveIn.DataAvailable += (sender, args) =>
            {
                try
                {
                    if (args.BytesRecorded <= 0)
                        return;

                    lock (writerLock)
                    {
                        if (!writerClosed)
                            fileWriter.Write(args.Buffer, 0, args.BytesRecorded);
                    }
                }
                catch (Exception ex)
                {
                    captureError ??= ex;
                }
            };

            waveIn.RecordingStopped += (sender, args) =>
            {
                if (args.Exception != null)
                    captureError ??= args.Exception;

                stopped.TrySetResult(true);
            };

            try
            {
                waveIn.StartRecording();
                recordingStarted = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Не удалось открыть микрофон: {ex.Message}");
                return false;
            }

            try
            {
                // Ждём отпускания клавиш
                while (IsKeyPressed(0x11) && IsKeyPressed(0x10))
                {
                    if (ct.IsCancellationRequested)
                        break;
                    await Task.Delay(100, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Ошибка записи: {ex.Message}");
            return false;
        }
        finally
        {
            if (waveIn != null)
            {
                if (recordingStarted)
                {
                    try
                    {
                        waveIn.StopRecording();
                        // Ждём завершения с таймаутом
                        var completed = await Task.WhenAny(stopped.Task, Task.Delay(2000));
                        if (completed != stopped.Task)
                            captureError ??= new IOException("Драйвер микрофона не завершил запись.");
                    }
                    catch (Exception ex)
                    {
                        captureError ??= ex;
                    }
                }

                lock (writerLock)
                {
                    writerClosed = true;
                    try { writer?.Dispose(); } catch { }
                }

                try { waveIn.Dispose(); } catch { }
            }

            lock (_recordLock)
            {
                _isRecording = false;
            }
        }

        if (!recordingStarted)
            return false;

        if (captureError != null)
        {
            Console.WriteLine($"⚠️ Микрофон недоступен: {captureError.Message}");
            return false;
        }

        try
        {
            return new FileInfo(outputPath).Length > 44;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExe,
            Arguments = $"\"{_pythonScriptPath}\" \"{wavPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        string? modelCache = GetModelCacheDirectory();
        if (modelCache != null && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HF_HOME")))
            psi.Environment["HF_HOME"] = modelCache;

        psi.Environment["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1";

        using var process = Process.Start(psi);
        if (process == null) return string.Empty;

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        string errors = await errorTask;
        if (errors.Contains("ModuleNotFoundError", StringComparison.OrdinalIgnoreCase) &&
            errors.Contains("faster_whisper", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("❌ Python-модуль faster-whisper не найден.");
            Console.WriteLine($"   Интерпретатор: {_pythonExe}");
            Console.WriteLine("   Запустите python_stt\\setup.bat или установите зависимости вручную.");
        }
        else if (!string.IsNullOrEmpty(errors) && !errors.Contains("symlinks", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"🐍 {errors}");
        }

        return (await outputTask).Trim();
    }

    private static string? GetModelCacheDirectory()
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fenrir",
                "cache");
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch
        {
            return null;
        }
    }
}