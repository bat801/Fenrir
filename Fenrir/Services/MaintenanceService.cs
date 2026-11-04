using System.Diagnostics;
using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

/// <summary>
/// Оркестратор генеральной уборки. Навыки не вызывают друг друга:
/// сервис последовательно запускает провайдеры и собирает общий отчёт.
/// </summary>
public sealed class MaintenanceService
{
    private readonly IReadOnlyList<ICleanupProvider> _providers;

    public MaintenanceService()
        : this(new ICleanupProvider[]
        {
            new TempCleanupProvider(),
            new AmdCacheCleanupProvider(),
            new CrashDumpCleanupProvider(),
            new WerCleanupProvider()
        })
    {
    }

    internal MaintenanceService(IEnumerable<ICleanupProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public MaintenanceReport Run(Action<string>? onStepStarting = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var steps = new List<MaintenanceStepResult>();

        foreach (ICleanupProvider provider in _providers)
        {
            onStepStarting?.Invoke(provider.Name);

            try
            {
                steps.Add(provider.Run());
            }
            catch (Exception ex)
            {
                steps.Add(new MaintenanceStepResult
                {
                    Name = provider.Name,
                    Status = MaintenanceStepStatus.Failed,
                    Error = ex.Message
                });
            }
        }

        stopwatch.Stop();

        return new MaintenanceReport
        {
            Steps = steps,
            Duration = stopwatch.Elapsed
        };
    }
}
