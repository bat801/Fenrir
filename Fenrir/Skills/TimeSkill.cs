using System.Globalization;
using Fenrir.Core.Models;
using Fenrir.Core.Routing;

namespace Fenrir.Core.Skills;

public class TimeSkill : ISkill
{
    public string Name => "time";

    public string Description => "Время и дата";

    public IReadOnlyList<SkillTrigger> Triggers { get; } = new[]
    {
        new SkillTrigger("который час", priority: 20),
        new SkillTrigger("сколько врем*", priority: 20),
        new SkillTrigger("какое врем*", priority: 20),
        new SkillTrigger("текущ* врем*", priority: 20),
        new SkillTrigger("какое сегодня числ*", priority: 20),
        new SkillTrigger("какая сегодня дат*", priority: 20),
        new SkillTrigger("какой сегодня день", priority: 20),
        new SkillTrigger("текущ* дат*", priority: 20),
        new SkillTrigger("сегодняшн* дат*", priority: 20),
        new SkillTrigger("день недел*", priority: 20)
    };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        var now = DateTime.Now;
        var tokens = TextNormalizer.Tokenize(command);

        if (TokenMatcher.ContainsAny(
                tokens,
                "числ*", "дат*",
                "день", "дня", "дню", "днем", "дне",
                "недел*"))
        {
            string dateStr = now.ToString("D", CultureInfo.GetCultureInfo("ru-RU"));
            return Task.FromResult(CommandResult.Ok($"Сегодня {dateStr}"));
        }

        if (TokenMatcher.ContainsAny(tokens, "врем*", "час", "часа", "часов"))
        {
            string timeStr = now.ToString("HH:mm");
            return Task.FromResult(CommandResult.Ok($"Сейчас {timeStr}"));
        }

        // По умолчанию показываем и дату, и время.
        return Task.FromResult(CommandResult.Ok($"{now:HH:mm}, {now:D}"));
    }
}
