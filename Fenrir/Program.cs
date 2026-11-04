using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Fenrir.Core.Models;
using Fenrir.Core.Routing;
using Fenrir.Core.Services;
using Fenrir.Core.Skills;

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

IReadOnlyList<ISkill> skills = CreateSkills();

if (TryHandleRouteDiagnostics(args, skills))
    return;

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
Console.WriteLine("   Команды: 'что ты умеешь' | 'очисти кэш амд' | 'генеральная уборка' | 'выход'\n");

var router = new IntentRouter(skills);

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

    if (IsHelpRequest(input))
    {
        PrintHelp(skills);
        speech.Speak("Вот что я умею.");
        continue;
    }

    RoutingResult routing = router.Route(input);

    if (routing.Best is null)
    {
        Console.WriteLine("🤔 Не понял команду. Скажите «что ты умеешь».");
        speech.Speak("Не понял команду.");
        continue;
    }

    ISkill matchedSkill;

    if (routing.IsAmbiguous)
    {
        ISkill? chosen = await ResolveAmbiguityAsync(
            routing,
            router,
            recognition,
            speech);

        if (chosen is null)
        {
            Console.WriteLine("❌ Уточнение отменено.");
            speech.Speak("Отменено.");
            continue;
        }

        matchedSkill = chosen;
    }
    else
    {
        matchedSkill = routing.Best.Skill;
    }

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
    speech.Speak(result.SpeechMessage ?? result.Message);
}

static IReadOnlyList<ISkill> CreateSkills() => new List<ISkill>
{
    new TimeSkill(),
    new SystemSkill(),
    new CalculatorSkill(),
    new BrowserSkill(),
    new CleanupSkill(),
    new AmdCacheSkill(),
    new DeepCleanSkill()
    // Сюда будем добавлять новые навыки
};

static bool TryHandleRouteDiagnostics(
    string[] args,
    IReadOnlyList<ISkill> skills)
{
    int index = Array.FindIndex(
        args,
        argument => argument.Equals("--route", StringComparison.OrdinalIgnoreCase));

    if (index < 0)
        return false;

    if (index + 1 >= args.Length)
    {
        Console.WriteLine("Использование: Fenrir.exe --route \"текст команды\"");
        return true;
    }

    string command = string.Join(" ", args.Skip(index + 1));
    var router = new IntentRouter(skills);
    RoutingResult result = router.Route(command);

    PrintRoutingResult(command, result);
    return true;
}

static void PrintRoutingResult(string command, RoutingResult result)
{
    Console.WriteLine($"🔎 Команда: {command}");
    Console.WriteLine($"   Нормализовано: {TextNormalizer.Normalize(command)}");

    if (result.Best is null)
    {
        Console.WriteLine("   Совпадений нет.");
        return;
    }

    Console.WriteLine(
        $"   Лучший навык: {result.Best.Skill.Name} " +
        $"({result.Best.Skill.Description})");
    Console.WriteLine(
        $"   Триггер: «{result.Best.Trigger.DisplayPhrase}», " +
        $"score {result.Best.Score}");

    if (result.IsAmbiguous)
        Console.WriteLine("   ⚠️ Неоднозначно: несколько навыков подходят одинаково.");

    Console.WriteLine("   Кандидаты:");
    foreach (SkillMatch candidate in result.Candidates)
    {
        Console.WriteLine(
            $"     - {candidate.Skill.Name}: " +
            $"«{candidate.Trigger.DisplayPhrase}», score {candidate.Score}");
    }
}

static async Task<ISkill?> ResolveAmbiguityAsync(
    RoutingResult routing,
    IntentRouter router,
    RecognitionService recognition,
    SpeechService speech)
{
    RoutingResult current = routing;

    while (true)
    {
        SkillMatch[] candidates = current.Candidates.Take(5).ToArray();

        Console.WriteLine("🤔 Уточните, что вы имели в виду:");
        for (int i = 0; i < candidates.Length; i++)
        {
            Console.WriteLine(
                $"   {i + 1}. {candidates[i].Skill.Description} — " +
                $"«{candidates[i].Trigger.DisplayPhrase}»");
        }

        Console.WriteLine("   Введите номер, другую команду или «отмена».");
        speech.SpeakSync("Уточните, пожалуйста, команду.");

        string answer = await ReadInputAsync(recognition);

        if (string.IsNullOrWhiteSpace(answer) || IsCancel(answer))
            return null;

        string trimmed = answer.Trim().TrimEnd('.');

        if (int.TryParse(trimmed, out int index) &&
            index >= 1 &&
            index <= candidates.Length)
        {
            return candidates[index - 1].Skill;
        }

        RoutingResult next = router.Route(answer);

        if (next.Best is null)
        {
            Console.WriteLine("🤔 Не понял. Введите номер из списка или «отмена».");
            speech.Speak("Не понял. Введите номер из списка.");
            continue;
        }

        if (!next.IsAmbiguous)
            return next.Best.Skill;

        current = next;
    }
}

static void PrintHelp(IReadOnlyList<ISkill> skills)
{
    Console.WriteLine("\n🤖 Что я умею:");

    foreach (ISkill skill in skills)
    {
        string example = skill.Triggers.Count > 0
            ? skill.Triggers[0].DisplayPhrase
            : "—";

        Console.WriteLine($"   • {skill.Description} — «{example}»");
    }

    Console.WriteLine("   • Список голосов — «список голосов»");
    Console.WriteLine("   • Выход — «выход»\n");
}

static bool IsHelpRequest(string input)
{
    string normalized = TextNormalizer.Normalize(input);

    return normalized is
        "помощь" or
        "help" or
        "команды" or
        "список команд" or
        "что ты умеешь" or
        "что умеешь";
}

static bool IsCancel(string input)
{
    string normalized = TextNormalizer.Normalize(input);

    return normalized is "отмена" or "отменить" or "нет" or "cancel";
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
    string normalized = TextNormalizer.Normalize(input);

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
