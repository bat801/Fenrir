using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class AmdCacheCleanupProvider : ICleanupProvider
{
    private readonly AmdCacheCleanupService _service;

    public AmdCacheCleanupProvider()
        : this(new AmdCacheCleanupService())
    {
    }

    internal AmdCacheCleanupProvider(AmdCacheCleanupService service)
    {
        _service = service;
    }

    public string Name => "Кэш AMD";

    public MaintenanceStepResult Run()
    {
        var report = _service.Clean();

        if (!report.CacheFound)
        {
            return new MaintenanceStepResult
            {
                Name = Name,
                Status = MaintenanceStepStatus.Skipped,
                Message = "Кэш AMD не найден"
            };
        }

        return new MaintenanceStepResult
        {
            Name = Name,
            Status = MaintenanceStepStatus.Completed,
            DeletedFiles = report.DeletedFiles,
            DeletedDirectories = report.DeletedDirectories,
            SkippedEntries = report.SkippedEntries,
            FreedBytes = report.FreedBytes,
            Warnings = report.Warnings
        };
    }
}
