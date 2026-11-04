using System.Data;
using System.Text.RegularExpressions;
using Fenrir.Core.Models;
using Fenrir.Core.Routing;

namespace Fenrir.Core.Skills;

public class CalculatorSkill : ISkill
{
    public string Name => "calculator";

    public string Description => "Вычисление математических выражений";

    public IReadOnlyList<SkillTrigger> Triggers { get; } = new[]
    {
        new SkillTrigger("сколько будет", priority: 20),
        new SkillTrigger("посчитай", priority: 20),
        new SkillTrigger("посчитать", priority: 20),
        new SkillTrigger("калькулятор", priority: 20),
        new SkillTrigger("вычисли", priority: 20),
        new SkillTrigger("реши пример", priority: 20)
    };

    public bool CanHandle(string command) => TryExtractExpression(command, out _);

    public Task<CommandResult> ExecuteAsync(string command)
    {
        if (!TryExtractExpression(command, out string expression))
            return Task.FromResult(CommandResult.Fail("Не нашёл математическое выражение в команде."));

        try
        {
            var dataTable = new DataTable();
            var result = dataTable.Compute(expression, null);
            return Task.FromResult(CommandResult.Ok($"{expression} = {result}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail(
                $"Ошибка при вычислении '{expression}': {ex.Message}"));
        }
    }

    private static bool TryExtractExpression(string command, out string expression)
    {
        expression = string.Empty;

        string prepared = ReplaceNumberWords(command);
        var match = Regex.Match(prepared, @"\d[\d\s+\-*\/\.\(\)]*");

        if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
            return false;

        expression = match.Value.Trim();
        return expression.Length > 0;
    }

    /// <summary>
    /// Заменяет русские слова-числа и операции на математические символы.
    /// "два плюс сто" → "2 + 100"
    /// </summary>
    private static string ReplaceNumberWords(string text)
    {
        // Удаляем слова-паразиты перед поиском выражения
        text = Regex.Replace(text, @"\bна\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bпод\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bиз\b", " ", RegexOptions.IgnoreCase);

        // Единицы
        text = Regex.Replace(text, @"\bноль\b", "0", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bодин\b", "1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bодна\b", "1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bдва\b", "2", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bдве\b", "2", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bтри\b", "3", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bчетыре\b", "4", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bпять\b", "5", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bшесть\b", "6", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bсемь\b", "7", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bвосемь\b", "8", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bдевять\b", "9", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bдесять\b", "10", RegexOptions.IgnoreCase);

        // Поддержка "плюс", "минус", "умножить", "разделить"
        text = Regex.Replace(text, @"\bплюс\b", "+", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bминус\b", "-", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bумножить\b", "*", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bразделить\b", "/", RegexOptions.IgnoreCase);

        // Вариации для Whisper
        text = Regex.Replace(text, @"\bумножаем\b", "*", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bделим\b", "/", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bподелить\b", "/", RegexOptions.IgnoreCase);

        return text;
    }
}
