using Fenrir.Core.Models;
using Fenrir.Core.Skills;
using System.Diagnostics;

namespace Fenrir.Core.Skills;

public class BrowserSkill : ISkill
{
    public string Name => "browser";
    public string[] Triggers => new[] { "браузер", "browser", "открой браузер", "запусти браузер", "яндекс", "хром", "yandex", "chrome" };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        string url = "https://yandex.ru"; // По умолчанию Яндекс

        // Простейший анализ: какое ключевое слово есть в команде?
        if (command.Contains("хром", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("chrome", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://google.com";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true // Открыть в браузере по умолчанию
            });
            return Task.FromResult(CommandResult.Ok($"Открываю {url}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Не удалось открыть браузер: {ex.Message}"));
        }
    }
}