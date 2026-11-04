using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class CrashDumpCleanupProvider : ICleanupProvider
{
    private readonly CrashDumpCleanupService _service;

    public CrashDumpCleanupProvider()
        : this(new CrashDumpCleanupService())
    {
    }

    internal CrashDumpCleanupProvider(CrashDumpCleanupService service)
    {
        _service = service;
    }

    public string Name => "Дампы падений";

    public MaintenanceStepResult Run()
    {
        if (!_service.HasTargets)
        {
            return new MaintenanceStepResult
            {
                Name = Name,
                Status = MaintenanceStepStatus.Skipped,
                Message = "Дампы падений не найдены"
            };
        }

        return MaintenanceStepResult.FromCleanupReport(Name, _service.Clean());
    }
}
