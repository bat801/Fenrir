using Fenrir.Core.Skills;
using Fenrir.Core.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("🤖 Фенрир запущен. Напишите 'выход' для завершения.\n");

// Регистрируем все имеющиеся навыки
var skills = new List<ISkill>
{
    new BrowserSkill(),
    new CalculatorSkill(),
    new TimeSkill(),
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

    // Ищем навык, который может обработать команду
    var matchedSkill = skills.FirstOrDefault(s =>
        s.Triggers.Any(t => input.Contains(t, StringComparison.OrdinalIgnoreCase)));

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