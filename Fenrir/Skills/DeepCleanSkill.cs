using Fenrir.Core.Models;
using Fenrir.Core.Services;
using Fenrir.Core.Utilities;

namespace Fenrir.Core.Skills;

public sealed class DeepCleanSkill : ISkill
{
    private readonly MaintenanceService _service;

    public DeepCleanSkill()
        : this(new MaintenanceService())
    {
    }

    internal DeepCleanSkill(MaintenanceService service)
    {
        _service = service;
    }

    public string Name => "deep-clean";

    public string[] Triggers => new[]
    {
        "генеральная уборка", "генеральную уборку", "генеральной уборкой",
        "полная уборка", "полную уборку", "полной уборкой",
        "полная очистка", "полную очистку", "полной очисткой",
        "сделай полную уборку", "сделай генеральную уборку", "сделай уборку",
        "очисти всё", "очисти все", "очистить всё", "очистить все",
        "очисти весь мусор", "очистить весь мусор", "убери весь мусор",
        "приберись", "прибери всё", "уберись", "прибраться"
    };

    public bool RequiresConfirmation => true;

    public string ConfirmationPrompt =>
        "Генеральная уборка очистит корзину, временные файлы, кэш AMD, дампы падений и отчёты об ошибках. " +
        "Действие необратимо. Продолжить?";

    public Task<CommandResult> ExecuteAsync(string command)
    {
        MaintenanceReport report = _service.Run(name => Console.WriteLine($"🧹 {name}..."));

        foreach (MaintenanceStepResult step in report.Steps)
        {
            Console.WriteLine(FormatStep(step));

            foreach (string warning in step.Warnings)
                Console.WriteLine($"   ⚠️ {warning}");
        }

        string message = BuildMessage(report);
        string speech = BuildSpeech(report);

        return Task.FromResult(report.FailedSteps > 0
            ? CommandResult.Fail(message, speech)
            : CommandResult.Ok(message, speech));
    }

    private static string FormatStep(MaintenanceStepResult step)
    {
        return step.Status switch
        {
            MaintenanceStepStatus.Skipped =>
                $"   ⏭ {step.Name}: {step.Message ?? "пропущено"}",
            MaintenanceStepStatus.Failed =>
                $"   ❌ {step.Name}: {step.Error ?? "неизвестная ошибка"}",
            _ => FormatCompletedStep(step)
        };
    }

    private static string FormatCompletedStep(MaintenanceStepResult step)
    {
        var parts = new List<string>();

        if (step.DeletedFiles > 0)
            parts.Add(RussianPlural.Files(step.DeletedFiles));

        if (step.DeletedDirectories > 0)
            parts.Add(RussianPlural.Folders(step.DeletedDirectories));

        if (step.FreedBytes > 0)
            parts.Add(ByteSizeFormatter.Format(step.FreedBytes));

        return parts.Count == 0
            ? $"   ✅ {step.Name}: нечего очищать"
            : $"   ✅ {step.Name}: {string.Join(", ", parts)}";
    }

    private static string BuildMessage(MaintenanceReport report)
    {
        var parts = new List<string>
        {
            $"Генеральная уборка завершена. Время: {FormatDuration(report.Duration)}."
        };

        if (report.HasChanges)
        {
            var cleaned = new List<string> { RussianPlural.Files(report.DeletedFiles) };
            if (report.DeletedDirectories > 0)
                cleaned.Add(RussianPlural.Folders(report.DeletedDirectories));

            parts.Add($"Удалено {string.Join(" и ", cleaned)}, освобождено {ByteSizeFormatter.Format(report.FreedBytes)}.");
        }
        else
        {
            parts.Add("Удалять было нечего.");
        }

        if (report.SkippedEntries > 0)
            parts.Add($"Пропущено {RussianPlural.Objects(report.SkippedEntries)}.");

        if (report.FailedSteps > 0)
            parts.Add($"Не удалось выполнить {RussianPlural.Steps(report.FailedSteps)}.");

        return string.Join(" ", parts);
    }

    private static string BuildSpeech(MaintenanceReport report)
    {
        var parts = new List<string> { "Генеральная уборка завершена." };

        if (report.HasChanges)
            parts.Add($"Освобождено {ByteSizeFormatter.Format(report.FreedBytes)}, удалено {RussianPlural.Files(report.DeletedFiles)}.");
        else
            parts.Add("Удалять было нечего.");

        if (report.FailedSteps > 0)
            parts.Add("Некоторые шаги не удались, подробности в консоли.");
        else if (report.SkippedEntries > 0)
            parts.Add($"Пропущено {RussianPlural.Objects(report.SkippedEntries)}.");

        return string.Join(" ", parts);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return "меньше секунды";

        if (duration.TotalMinutes < 1)
            return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))} сек.";

        return $"{(int)duration.TotalMinutes} мин {duration.Seconds} сек.";
    }
}
