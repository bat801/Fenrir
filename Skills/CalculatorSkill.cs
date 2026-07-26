using Fenrir.Core.Models;
using Fenrir.Core.Skills;
using System.Data;
using System.Text.RegularExpressions;

namespace Fenrir.Core.Skills;

public class CalculatorSkill : ISkill
{
    public string Name => "calculator";
    public string[] Triggers => new[]
    {
        "сколько будет", "посчитай", "калькулятор", "вычисли",
        "реши пример", "сколько", "посчитать", "реши"
    };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        // Ищем математическое выражение в тексте команды
        // Поддерживаем: цифры, +, -, *, /, ., пробелы, скобки
        var match = Regex.Match(command, @"[\d\s\+\-\*\/\.\(\)]+");

        if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
        {
            return Task.FromResult(CommandResult.Fail("Не нашёл математическое выражение в команде."));
        }

        string expression = match.Value.Trim();

        try
        {
            // DataTable.Compute — простой и безопасный способ вычислить строку
            var dataTable = new DataTable();
            var result = dataTable.Compute(expression, null);
            return Task.FromResult(CommandResult.Ok($"{expression} = {result}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка при вычислении '{expression}': {ex.Message}"));
        }
    }
}