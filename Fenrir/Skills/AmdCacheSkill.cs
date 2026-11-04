using Fenrir.Core.Models;
using Fenrir.Core.Services;
using Fenrir.Core.Utilities;

namespace Fenrir.Core.Skills;

public sealed class AmdCacheSkill : ISkill
{
    private readonly AmdCacheCleanupService _service;

    public AmdCacheSkill()
        : this(new AmdCacheCleanupService())
    {
    }

    internal AmdCacheSkill(AmdCacheCleanupService service)
    {
        _service = service;
    }

    public string Name => "amd-cache";

    public string[] Triggers => new[]
    {
        "очисти кэш амд", "очисти кэш amd", "очистить кэш амд", "очистить кэш amd",
        "почисти кэш амд", "почисти кэш amd", "почистить кэш амд", "почистить кэш amd",
        "очисти amd кэш", "очисти amd-кэш", "почисти amd кэш",
        "очисти кэш видеокарты", "почисти кэш видеокарты", "кэш видеокарты",
        "очисти шейдерный кэш", "шейдерный кэш", "кэш шейдеров",
        "amd cache", "очисти кэш", "почисти кэш", "очистить кэш"
    };

    public bool RequiresConfirmation => true;

    public string ConfirmationPrompt =>
        "Очистить кэш шейдеров AMD? При следующем запуске игр он создастся заново. Скажите да или нет.";

    public Task<CommandResult> ExecuteAsync(string command)
    {
        if (IsUnsupportedCacheCommand(command))
        {
            const string unsupported = "Пока я умею очищать только кэш AMD. Скажите: «очисти кэш амд».";
            return Task.FromResult(CommandResult.Fail(unsupported, unsupported));
        }

        AmdCacheReport report = _service.Clean();

        foreach (string warning in report.Warnings)
            Console.WriteLine($"⚠️ {warning}");

        if (!report.CacheFound)
        {
            const string notFound = "Кэш AMD не найден. Возможно, у вас не AMD или кэш ещё не создавался.";
            return Task.FromResult(CommandResult.Ok(notFound, "Кэш AMD не найден."));
        }

        if (!report.HasChanges)
        {
            if (report.SkippedEntries > 0)
            {
                string failed = $"Кэш AMD не удалось очистить: {RussianPlural.Objects(report.SkippedEntries)} заняты или недоступны.";
                return Task.FromResult(CommandResult.Fail(failed, failed));
            }

            const string empty = "Кэш AMD уже пуст.";
            return Task.FromResult(CommandResult.Ok(empty, empty));
        }

        var parts = new List<string> { RussianPlural.Files(report.DeletedFiles) };
        if (report.DeletedDirectories > 0)
            parts.Add(RussianPlural.Folders(report.DeletedDirectories));

        string size = ByteSizeFormatter.Format(report.FreedBytes);
        string text = $"Кэш AMD очищен. Удалено {string.Join(" и ", parts)}, освобождено {size}.";

        if (report.SkippedEntries > 0)
            text += $" Пропущено {RussianPlural.Objects(report.SkippedEntries)}: файлы заняты или недоступны.";

        string speech = $"Кэш AMD очищен. Освобождено {size}.";
        if (report.SkippedEntries > 0)
            speech += $" Пропущено {RussianPlural.Objects(report.SkippedEntries)}.";

        return Task.FromResult(CommandResult.Ok(text, speech));
    }

    private static bool IsUnsupportedCacheCommand(string command)
    {
        string[] unsupported =
        {
            "браузер", "chrome", "хром", "yandex", "яндекс", "edge", "firefox",
            "windows", "виндовс", "обновлен",
            "nvidia", "нвидиа", "intel", "интел"
        };

        return unsupported.Any(word => command.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
