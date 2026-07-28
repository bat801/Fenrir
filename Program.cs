using Fenrir.Core.Skills;
using Fenrir.Core.Models;
using System.Security.Principal;

Console.OutputEncoding = System.Text.Encoding.UTF8;

bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);

if (!isAdmin)
{
    Console.WriteLine("⚠️ Fenrir запущен без прав администратора.");
    Console.WriteLine("   Некоторые команды (диспетчер задач, выключение) могут не работать.\n");
}

Console.WriteLine("🤖 Фенрир запущен. Напишите 'выход' для завершения.\n");

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
        Console.WriteLine("До свидания, сэр.");
        break;
    }

    // Ищем навык с самым длинным совпавшим триггером, который может обработать команду
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
    }
    else
    {
        Console.WriteLine("🤔 Я пока не знаю такой команды.");
    }
}