using System.Runtime.Versioning;
using System.Security.Principal;
using Fenrir.Core.Services;
using Fenrir.Core.Skills;
using Fenrir.Core.Models;

[assembly: SupportedOSPlatform("windows")]

bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (!isAdmin)
{
    Console.WriteLine("⚠️ Fenrir запущен без прав администратора.");
    Console.WriteLine("   Некоторые команды (диспетчер задач, выключение) могут не работать.\n");
}

// Инициализация TTS
var speech = new SpeechService();

// Инициализация STT
var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "python_stt", "listen.py");
pythonScriptPath = Path.GetFullPath(pythonScriptPath);
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
    // Сюда будем добавлять новые навыки
};

// Главный цикл обработки команд
while (true)
{
    Console.Write("> ");

    // Запускаем параллельно: чтение с клавиатуры И ожидание голоса
    string? input = null;
    var consoleInputTask = Task.Run(() => Console.ReadLine() ?? string.Empty);
    var voiceInputTask = recognition.ListenAsync();

    // Ждём, что произойдёт первым: ввод с клавиатуры или голос
    var completedTask = await Task.WhenAny(consoleInputTask, voiceInputTask);

    if (completedTask == consoleInputTask)
    {
        input = await consoleInputTask;
    }
    else
    {
        input = await voiceInputTask;
    }

    if (string.IsNullOrWhiteSpace(input))
        continue;

    Console.WriteLine(input); // Показываем, что распознано

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