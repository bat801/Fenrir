using Fenrir.Core.Models;
using Fenrir.Core.Services;

namespace Fenrir.Tests;

public sealed class MaintenanceServiceTests
{
    [Fact]
    public void Run_AggregatesSteps()
    {
        var service = new MaintenanceService(new ICleanupProvider[]
        {
            new FakeProvider("A", () => new MaintenanceStepResult
            {
                Name = "A",
                DeletedFiles = 2,
                DeletedDirectories = 1,
                FreedBytes = 100
            }),
            new FakeProvider("B", () => new MaintenanceStepResult
            {
                Name = "B",
                DeletedFiles = 3,
                SkippedEntries = 1,
                FreedBytes = 200
            })
        });

        MaintenanceReport report = service.Run();

        Assert.Equal(5, report.DeletedFiles);
        Assert.Equal(1, report.DeletedDirectories);
        Assert.Equal(1, report.SkippedEntries);
        Assert.Equal(300, report.FreedBytes);
        Assert.Equal(2, report.CompletedSteps);
        Assert.Equal(0, report.FailedSteps);
        Assert.True(report.HasChanges);
    }

    [Fact]
    public void Run_ContinuesWhenProviderThrows()
    {
        var service = new MaintenanceService(new ICleanupProvider[]
        {
            new FakeProvider("bad", () => throw new InvalidOperationException("boom")),
            new FakeProvider("good", () => new MaintenanceStepResult
            {
                Name = "good",
                DeletedFiles = 4,
                FreedBytes = 400
            })
        });

        MaintenanceReport report = service.Run();

        Assert.Equal(2, report.Steps.Count);
        Assert.Equal(1, report.FailedSteps);
        Assert.Equal(1, report.CompletedSteps);
        Assert.Equal(4, report.DeletedFiles);
        Assert.Equal(400, report.FreedBytes);
        Assert.Equal("boom", report.Steps[0].Error);
    }

    [Fact]
    public void Run_ReportsProgress()
    {
        var started = new List<string>();
        var service = new MaintenanceService(new ICleanupProvider[]
        {
            new FakeProvider("A", () => new MaintenanceStepResult { Name = "A" }),
            new FakeProvider("B", () => new MaintenanceStepResult { Name = "B" })
        });

        service.Run(started.Add);

        Assert.Equal(new[] { "A", "B" }, started);
    }

    [Fact]
    public void Run_KeepsSkippedStepsOutOfTotals()
    {
        var service = new MaintenanceService(new ICleanupProvider[]
        {
            new FakeProvider("skip", () => new MaintenanceStepResult
            {
                Name = "skip",
                Status = MaintenanceStepStatus.Skipped,
                Message = "nothing to do"
            })
        });

        MaintenanceReport report = service.Run();

        Assert.Equal(1, report.SkippedSteps);
        Assert.Equal(0, report.CompletedSteps);
        Assert.False(report.HasChanges);
    }

    private sealed class FakeProvider : ICleanupProvider
    {
        private readonly Func<MaintenanceStepResult> _run;

        public FakeProvider(string name, Func<MaintenanceStepResult> run)
        {
            Name = name;
            _run = run;
        }

        public string Name { get; }

        public MaintenanceStepResult Run() => _run();
    }
}
