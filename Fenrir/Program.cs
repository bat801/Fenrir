using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Fenrir.Core.Services;
using Fenrir.Core.Skills;
using Fenrir.Core.Models;

[assembly: SupportedOSPlatform("windows")]

bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);

Console.OutputEncoding = System.Text.Encoding.UTF8;

var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "python_stt", "listen.py");

if (args.Contains("--diagnose", StringComparer.OrdinalIgnoreCase))
{
    string pythonExe = RecognitionService.ResolvePythonExecutable();
    Console.WriteLine($"📦 Exe: {Environment.ProcessPath}");
    Console.WriteLine($"📁 Base directory: {AppContext.BaseDirectory}");
    Console.WriteLine($"🐍 Python script: {pythonScriptPath}");
    Console.WriteLine($"📁 Script exists: {File.Exists(pythonScriptPath)}");
    Console.WriteLine($"🐍 Python: {pythonExe}");
    PrintPythonCheck(pythonExe);
    return;
}

if (!isAdmin)
{
    Console.WriteLine("⚠️ Fenrir запущен без прав администратора.");
    Console.WriteLine("   Некоторые команды (диспетчер задач, выключение) могут не работать.\n");
}

// Инициализация TTS
var speech = new SpeechService();

// Инициализация STT
Console.WriteLine($"📦 Exe: {Environment.ProcessPath}");
Console.WriteLine($"🐍 Python script path: {pythonScriptPath}");
Console.WriteLine($"📁 File exists: {File.Exists(pythonScriptPath)}");
var recognition = new RecognitionService(pythonScriptPath);

speech.Speak("Фенрир запущен. Я готов к работе, сэр.");

Console.WriteLine("\n🤖 Фенрир запущен. Напишите 'выход' для завершения.");
Console.WriteLine("   Голос: зажмите Ctrl+Shift, говорите, отпустите — Fenrir выполнит команду.");
Console.WriteLine("   Текст: просто напишите команду и нажмите Enter.");
Console.WriteLine("   Команды: 'список голосов' | 'голос <номер>' | 'выход'\n");

// Регистрируем все имеющиеся навыки
var skills = new List<ISkill>
{
    new TimeSkill(),
    new SystemSkill(),
    new CalculatorSkill(),
    new BrowserSkill(),
    new CleanupSkill(),
    // Сюда будем добавлять новые навыки
};

// Главный цикл обработки команд
while (true)
{
    string? input = await ReadInputAsync(recognition);

    if (string.IsNullOrWhiteSpace(input))
        continue;

    // Системные команды
    if (input.Equals("выход", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        speech.Speak("До свидания, сэр.");
        Console.WriteLine("До свидания, сэр.");
        break;
    }

    if (input.Equals("список голосов", StringComparison.OrdinalIgnoreCase))
    {
        speech.ListVoices();
        continue;
    }

    // Умный роутер: ищем навык с самым длинным совпавшим триггером, который может обработать команду
    ISkill? matchedSkill = null;
    int bestTriggerLength = 0;

    foreach (var skill in skills)
    {
        foreach (var trigger in skill.Triggers)
        {
            if (input.Contains(trigger, StringComparison.OrdinalIgnoreCase))
            {
                if (trigger.Length > bestTriggerLength)
                {
                    bestTriggerLength = trigger.Length;
                    matchedSkill = skill;
                }
            }
        }
    }

    if (matchedSkill != null)
    {
        if (matchedSkill.RequiresConfirmation)
        {
            Console.WriteLine($"⚠️ {matchedSkill.ConfirmationPrompt}");
            speech.SpeakSync(matchedSkill.ConfirmationPrompt);

            string confirmation = await ReadInputAsync(recognition);
            if (!IsConfirmationYes(confirmation))
            {
                Console.WriteLine("❌ Действие отменено.");
                speech.Speak("Действие отменено.");
                continue;
            }
        }

        var result = await matchedSkill.ExecuteAsync(input);
        Console.WriteLine(result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}");
        speech.Speak(result.Message);
    }
    else
    {
        Console.WriteLine("🤔 Я пока не знаю такой команды.");
        speech.Speak("Я пока не знаю такой команды.");
    }
}

static async Task<string> ReadInputAsync(RecognitionService recognition)
{
    Console.Write("> ");

    using var cts = new CancellationTokenSource();

    var keyboardTask = Task.Run(() =>
    {
        var line = new System.Text.StringBuilder();
        while (!cts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return line.ToString().Trim();
                }
                else if (key.Key == ConsoleKey.Backspace && line.Length > 0)
                {
                    line.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    line.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
            else
            {
                Thread.Sleep(50);
            }
        }
        return string.Empty;
    }, cts.Token);

    var voiceTask = recognition.ListenAsync(cts.Token);
    var completedTask = await Task.WhenAny(keyboardTask, voiceTask);

    string input;
    if (completedTask == keyboardTask)
    {
        cts.Cancel();
        input = await keyboardTask;
    }
    else
    {
        input = await voiceTask;
        cts.Cancel();

        await Task.Delay(100);
        while (Console.KeyAvailable)
            Console.ReadKey(true);

        if (!string.IsNullOrWhiteSpace(input))
            Console.WriteLine(input);
    }

    return input.TrimEnd('.').Trim();
}

static bool IsConfirmationYes(string input)
{
    string normalized = input.Trim().ToLowerInvariant();

    return normalized is "да" or "yes" or "ага" or "угу" or "ок" or "окей"
        or "давай" or "конечно" or "подтверждаю" or "подтвердить";
}

static void PrintPythonCheck(string pythonExe)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = "-c \"import faster_whisper, av; print(faster_whisper.__version__ + ', av ' + av.__version__)\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            Console.WriteLine("❌ Не удалось запустить Python.");
            return;
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
            Console.WriteLine($"✅ faster-whisper: {output.Trim()}");
        else
            Console.WriteLine($"❌ faster-whisper недоступен:\n{error.Trim()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка запуска Python: {ex.Message}");
    }
}