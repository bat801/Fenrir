using System.Diagnostics;
using Fenrir.Core.Models;
using Fenrir.Core.Routing;

namespace Fenrir.Core.Skills;

public class BrowserSkill : ISkill
{
    public string Name => "browser";

    public string Description => "Открытие браузера";

    public IReadOnlyList<SkillTrigger> Triggers { get; } = new[]
    {
        new SkillTrigger("открой браузер", priority: 20),
        new SkillTrigger("запусти браузер", priority: 20),
        new SkillTrigger("открой хром", priority: 20),
        new SkillTrigger("запусти хром", priority: 20),
        new SkillTrigger("открой яндекс", priority: 20),
        new SkillTrigger("запусти яндекс", priority: 20),
        new SkillTrigger("браузер", priority: 10),
        new SkillTrigger("browser", priority: 10)
    };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        var tokens = TextNormalizer.Tokenize(command);

        string url = TokenMatcher.ContainsAny(tokens, "хром", "chrome")
            ? "https://google.com"
            : "https://yandex.ru";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return Task.FromResult(CommandResult.Ok($"Открываю {url}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail(
                $"Не удалось открыть браузер: {ex.Message}"));
        }
    }
}
