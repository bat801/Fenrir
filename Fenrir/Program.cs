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

    string? input = null;

    // Ждём либо клавиатуру, либо голос — что произойдёт раньше
    using var cts = new CancellationTokenSource();

    // Задача: ждём нажатия Enter (клавиатурный ввод)
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
                    Console.WriteLine(); // Переход на новую строку
                    return line.ToString().Trim();
                }
                else if (key.Key == ConsoleKey.Backspace && line.Length > 0)
                {
                    line.Length--;
                    Console.Write("\b \b"); // Стираем символ
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    line.Append(key.KeyChar);
                    Console.Write(key.KeyChar); // Эхо-вывод
                }
            }
            else
            {
                Thread.Sleep(50);
            }
        }
        return string.Empty;
    }, cts.Token);

    // Задача: ждём голосовой ввод
    var voiceTask = recognition.ListenAsync(cts.Token);

    // Ждём, что произойдёт раньше
    var completedTask = await Task.WhenAny(keyboardTask, voiceTask);

    if (completedTask == keyboardTask)
    {
        // Клавиатура победила — отменяем голос
        cts.Cancel();
        input = await keyboardTask;
    }
    else
    {
        // Голос победил
        input = await voiceTask;
        cts.Cancel(); // Отменяем ожидание клавиатуры

        // Очищаем буфер консоли от случайных нажатий во время голосового ввода
        await Task.Delay(100);
        while (Console.KeyAvailable)
            Console.ReadKey(true);
    }

    if (string.IsNullOrWhiteSpace(input))
        continue;

    // Убираем точку в конце
    input = input.TrimEnd('.').Trim();

    // Показываем что распознано (для голосового ввода уже показано, но для единообразия)
    if (completedTask == voiceTask)
        Console.WriteLine(input);

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