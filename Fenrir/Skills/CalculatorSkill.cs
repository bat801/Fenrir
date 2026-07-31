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
        // Сначала заменяем слова-числа на цифры
        command = ReplaceNumberWords(command);

        // Ищем математическое выражение
        var match = Regex.Match(command, @"\d[\d\s+\-*\/\.\(\)]*");

        if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
        {
            return Task.FromResult(CommandResult.Fail("Не нашёл математическое выражение в команде."));
        }

        string expression = match.Value.Trim();

        try
        {
            var dataTable = new DataTable();
            var result = dataTable.Compute(expression, null);
            return Task.FromResult(CommandResult.Ok($"{expression} = {result}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка при вычислении '{expression}': {ex.Message}"));
        }
    }

    /// <summary>
    /// Заменяет русские слова-числа на цифры.
    /// "два плюс сто" → "2 + 100"
    /// </summary>
    private string ReplaceNumberWords(string text)
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