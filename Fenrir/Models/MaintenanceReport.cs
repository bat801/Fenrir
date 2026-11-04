namespace Fenrir.Core.Models;

public enum MaintenanceStepStatus
{
    Completed,
    Skipped,
    Failed
}

public sealed class MaintenanceStepResult
{
    public required string Name { get; init; }
    public MaintenanceStepStatus Status { get; init; } = MaintenanceStepStatus.Completed;
    public int DeletedFiles { get; init; }
    public int DeletedDirectories { get; init; }
    public int SkippedEntries { get; init; }
    public long FreedBytes { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static MaintenanceStepResult FromCleanupReport(
        string name,
        CleanupReport report,
        string? message = null)
    {
        return new MaintenanceStepResult
        {
            Name = name,
            Status = MaintenanceStepStatus.Completed,
            DeletedFiles = report.DeletedFiles,
            DeletedDirectories = report.DeletedDirectories,
            SkippedEntries = report.SkippedEntries,
            FreedBytes = report.FreedBytes,
            Message = message,
            Warnings = report.Warnings.ToArray()
        };
    }
}

public sealed class MaintenanceReport
{
    public IReadOnlyList<MaintenanceStepResult> Steps { get; init; } = Array.Empty<MaintenanceStepResult>();
    public TimeSpan Duration { get; init; }

    public int DeletedFiles => Steps.Sum(step => step.DeletedFiles);
    public int DeletedDirectories => Steps.Sum(step => step.DeletedDirectories);
    public int SkippedEntries => Steps.Sum(step => step.SkippedEntries);
    public long FreedBytes => Steps.Sum(step => step.FreedBytes);
    public int CompletedSteps => Steps.Count(step => step.Status == MaintenanceStepStatus.Completed);
    public int SkippedSteps => Steps.Count(step => step.Status == MaintenanceStepStatus.Skipped);
    public int FailedSteps => Steps.Count(step => step.Status == MaintenanceStepStatus.Failed);

    public bool HasChanges => DeletedFiles > 0 || DeletedDirectories > 0;
}
