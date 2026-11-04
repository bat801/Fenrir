using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class WerCleanupProvider : ICleanupProvider
{
    private readonly WerCleanupService _service;

    public WerCleanupProvider()
        : this(new WerCleanupService())
    {
    }

    internal WerCleanupProvider(WerCleanupService service)
    {
        _service = service;
    }

    public string Name => "Отчёты об ошибках Windows";

    public MaintenanceStepResult Run()
    {
        if (!_service.HasTargets)
        {
            return new MaintenanceStepResult
            {
                Name = Name,
                Status = MaintenanceStepStatus.Skipped,
                Message = "Отчёты об ошибках не найдены"
            };
        }

        return MaintenanceStepResult.FromCleanupReport(Name, _service.Clean());
    }
}
