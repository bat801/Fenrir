using Fenrir.Core.Services;
using Fenrir.Core.Skills;
using Fenrir.Core.Models;
using System.Security.Principal;

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
speech.Speak("Фенрир запущен. Я готов к работе, сэр.");

Console.WriteLine("\n🤖 Фенрир запущен. Напишите 'выход' для завершения.");
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
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

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