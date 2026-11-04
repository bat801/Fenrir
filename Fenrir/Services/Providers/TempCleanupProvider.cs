using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class TempCleanupProvider : ICleanupProvider
{
    private readonly CleanupService _service;

    public TempCleanupProvider()
        : this(new CleanupService())
    {
    }

    internal TempCleanupProvider(CleanupService service)
    {
        _service = service;
    }

    public string Name => "Корзина и временные файлы";

    public MaintenanceStepResult Run()
    {
        var report = _service.Clean();
        var warnings = report.Warnings.ToList();

        if (!report.RecycleBinCleared)
            warnings.Add($"Корзина: {report.RecycleBinError ?? "не удалось очистить"}");

        return new MaintenanceStepResult
        {
            Name = Name,
            Status = MaintenanceStepStatus.Completed,
            DeletedFiles = report.DeletedFiles,
            DeletedDirectories = report.DeletedDirectories,
            SkippedEntries = report.SkippedEntries,
            FreedBytes = report.FreedBytes,
            Warnings = warnings
        };
    }
}
