using Fenrir.Core.Models;
using Fenrir.Core.Skills;

namespace Fenrir.Core.Skills;

public class TimeSkill : ISkill
{
    public string Name => "time";
    public string[] Triggers => new[]
    {
        "который час", "сколько времени", "какое сегодня число",
        "какой сегодня день", "дата", "время", "день недели"
    };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        var now = DateTime.Now;

        if (command.Contains("число", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("дата", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("день", StringComparison.OrdinalIgnoreCase))
        {
            string dateStr = now.ToString("D", new System.Globalization.CultureInfo("ru-RU"));
            return Task.FromResult(CommandResult.Ok($"Сегодня {dateStr}"));
        }

        if (command.Contains("врем", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("час", StringComparison.OrdinalIgnoreCase))
        {
            string timeStr = now.ToString("HH:mm");
            return Task.FromResult(CommandResult.Ok($"Сейчас {timeStr}"));
        }

        // По умолчанию показываем и дату, и время
        return Task.FromResult(CommandResult.Ok($"{now:HH:mm}, {now:D}"));
    }
}