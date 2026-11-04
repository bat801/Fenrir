using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public interface ICleanupProvider
{
    string Name { get; }
    MaintenanceStepResult Run();
}
