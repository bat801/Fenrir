using Fenrir.Core.Models;
using Fenrir.Core.Routing;
using Fenrir.Core.Services;
using Fenrir.Core.Utilities;

namespace Fenrir.Core.Skills;

public sealed class AmdCacheSkill : ISkill
{
    private static readonly string[] UnsupportedCachePatterns =
    {
        "браузер*", "chrome*", "хром*", "yandex*", "яндекс*",
        "edge*", "firefox*", "opera*", "опер*", "safari*", "сафари*",
        "windows*", "виндовс*", "обновлен*",
        "nvidia*", "нвидиа*", "intel*", "интел*",
        "android*", "телефон*"
    };

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

    public string Description => "Очистка кэша шейдеров AMD";

    public IReadOnlyList<SkillTrigger> Triggers { get; } = new[]
    {
        CacheTrigger("очисти кэш амд", priority: 30),
        CacheTrigger("очисти кэш amd", priority: 30),
        CacheTrigger("очистить кэш амд", priority: 30),
        CacheTrigger("очистить кэш amd", priority: 30),
        CacheTrigger("почисти кэш амд", priority: 30),
        CacheTrigger("почисти кэш amd", priority: 30),
        CacheTrigger("почистить кэш амд", priority: 30),
        CacheTrigger("почистить кэш amd", priority: 30),
        CacheTrigger("очисти amd кэш", priority: 30),
        CacheTrigger("почисти amd кэш", priority: 30),
        CacheTrigger("очисти кэш видеокарты", priority: 30),
        CacheTrigger("почисти кэш видеокарты", priority: 30),
        CacheTrigger("кэш видеокарты", priority: 20),
        CacheTrigger("очисти шейдерный кэш", priority: 30),
        CacheTrigger("шейдерный кэш", priority: 20),
        CacheTrigger("кэш шейдеров", priority: 20),
        CacheTrigger("amd cache", priority: 20),
        CacheTrigger("очисти кэш", priority: 20),
        CacheTrigger("почисти кэш", priority: 20),
        CacheTrigger("очистить кэш", priority: 20),
        CacheTrigger("очистка кэш*", priority: 20)
    };

    public bool RequiresConfirmation => true;

    public string ConfirmationPrompt =>
        "Очистить кэш шейдеров AMD? При следующем запуске игр он создастся заново. Скажите да или нет.";

    public bool CanHandle(string command)
    {
        if (IsUnsupportedCacheCommand(command))
            return false;

        var tokens = TextNormalizer.Tokenize(command);
        return TokenMatcher.ContainsAny(tokens, "кэш*", "cache");
    }

    public Task<CommandResult> ExecuteAsync(string command)
    {
        if (IsUnsupportedCacheCommand(command))
        {
            const string unsupported =
                "Пока я умею очищать только кэш AMD. Скажите: «очисти кэш амд».";
            return Task.FromResult(CommandResult.Fail(unsupported, unsupported));
        }

        AmdCacheReport report = _service.Clean();

        foreach (string warning in report.Warnings)
            Console.WriteLine($"⚠️ {warning}");

        if (!report.CacheFound)
        {
            const string notFound =
                "Кэш AMD не найден. Возможно, у вас не AMD или кэш ещё не создавался.";
            return Task.FromResult(CommandResult.Ok(notFound, "Кэш AMD не найден."));
        }

        if (!report.HasChanges)
        {
            if (report.SkippedEntries > 0)
            {
                string failed =
                    $"Кэш AMD не удалось очистить: {RussianPlural.Objects(report.SkippedEntries)} заняты или недоступны.";
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

    private static SkillTrigger CacheTrigger(string phrase, int priority)
        => new(phrase, priority, excludes: UnsupportedCachePatterns);

    private static bool IsUnsupportedCacheCommand(string command)
    {
        var tokens = TextNormalizer.Tokenize(command);
        return TokenMatcher.ContainsAny(tokens, UnsupportedCachePatterns);
    }
}
