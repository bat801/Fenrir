using System.Runtime.Versioning;
using Fenrir.Core.Models;
using Fenrir.Core.Services;
using Fenrir.Core.Skills;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class CleanupSkillsTests : IDisposable
{
    private readonly string _baseDirectory = Path.Combine(
        Path.GetTempPath(),
        "FenrirTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void AmdCacheSkill_RequiresConfirmationAndSupportsExpectedPhrases()
    {
        var skill = new AmdCacheSkill();

        Assert.True(skill.RequiresConfirmation);
        Assert.False(string.IsNullOrWhiteSpace(skill.ConfirmationPrompt));
        Assert.Contains(skill.Triggers, trigger => trigger.Phrase == "очисти кэш");
        Assert.Contains(skill.Triggers, trigger => trigger.Phrase == "очисти кэш амд");
        Assert.Contains(skill.Triggers, trigger => trigger.Phrase == "очисти кэш видеокарты");
    }

    [Fact]
    public async Task AmdCacheSkill_ReportsMissingCacheWithoutFailure()
    {
        var service = new AmdCacheCleanupService(Path.Combine(_baseDirectory, "AMD"));
        var skill = new AmdCacheSkill(service);

        var result = await skill.ExecuteAsync("очисти кэш");

        Assert.True(result.Success);
        Assert.Contains("не найден", result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.SpeechMessage));
    }

    [Fact]
    public async Task AmdCacheSkill_DoesNotTouchCacheForBrowserRequest()
    {
        string amdRoot = Path.Combine(_baseDirectory, "AMD");
        string dxCache = Path.Combine(amdRoot, "DxCache");
        Directory.CreateDirectory(dxCache);
        string cacheFile = Path.Combine(dxCache, "shader.bin");
        File.WriteAllText(cacheFile, "shader");

        var skill = new AmdCacheSkill(new AmdCacheCleanupService(amdRoot));

        var result = await skill.ExecuteAsync("очисти кэш браузера");

        Assert.False(result.Success);
        Assert.Contains("только кэш AMD", result.Message);
        Assert.True(File.Exists(cacheFile));
    }

    [Fact]
    public void DeepCleanSkill_RequiresConfirmationAndSupportsExpectedPhrases()
    {
        var skill = new DeepCleanSkill();

        Assert.True(skill.RequiresConfirmation);
        Assert.Contains(skill.Triggers, trigger => trigger.DisplayPhrase == "генеральн* уборк*");
        Assert.Contains(skill.Triggers, trigger => trigger.DisplayPhrase == "полн* уборк*");
        Assert.Contains(skill.Triggers, trigger => trigger.DisplayPhrase == "очисти всё");
        Assert.Contains(skill.Triggers, trigger => trigger.DisplayPhrase == "приберись");
    }

    [Fact]
    public void DeepCleanSkill_ConfirmationMentionsEveryTarget()
    {
        var skill = new DeepCleanSkill();

        Assert.Contains("корзину", skill.ConfirmationPrompt);
        Assert.Contains("временные файлы", skill.ConfirmationPrompt);
        Assert.Contains("кэш AMD", skill.ConfirmationPrompt);
        Assert.Contains("дампы", skill.ConfirmationPrompt);
        Assert.Contains("отчёты", skill.ConfirmationPrompt);
    }

    [Fact]
    public async Task DeepCleanSkill_AggregatesResultsAndReportsFailedSteps()
    {
        var maintenance = new MaintenanceService(new ICleanupProvider[]
        {
            new FakeProvider("Кэш AMD", () => new MaintenanceStepResult
            {
                Name = "Кэш AMD",
                DeletedFiles = 2,
                FreedBytes = 1024
            }),
            new FakeProvider("Отчёты", () => throw new InvalidOperationException("access denied"))
        });

        var skill = new DeepCleanSkill(maintenance);

        var result = await skill.ExecuteAsync("генеральная уборка");

        Assert.False(result.Success);
        Assert.Contains("Удалено 2 файла", result.Message);
        Assert.Contains("Не удалось выполнить 1 шаг", result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.SpeechMessage));
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDirectory))
                Directory.Delete(_baseDirectory, recursive: true);
        }
        catch
        {
        }
    }
}
