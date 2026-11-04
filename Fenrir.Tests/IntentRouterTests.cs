using Fenrir.Core.Models;
using Fenrir.Core.Routing;
using Fenrir.Core.Skills;

namespace Fenrir.Tests;

public sealed class IntentRouterTests
{
    private static IReadOnlyList<ISkill> CreateSkills() => new ISkill[]
    {
        new TimeSkill(),
        new SystemSkill(),
        new CalculatorSkill(),
        new BrowserSkill(),
        new CleanupSkill(),
        new AmdCacheSkill(),
        new DeepCleanSkill()
    };

    private static string? RouteName(string input)
    {
        RoutingResult result = new IntentRouter(CreateSkills()).Route(input);
        return result.Best?.Skill.Name;
    }

    [Theory]
    [InlineData("включи персонажа", null)]
    [InlineData("расскажи про персону", null)]
    [InlineData("сколько времени", "time")]
    [InlineData("сколько будет 2 плюс 2", "calculator")]
    [InlineData("посчитай", null)]
    [InlineData("очисти кэш амд", "amd-cache")]
    [InlineData("очисти кэш браузера", null)]
    [InlineData("очисти кэш хрома", null)]
    [InlineData("очисти мусор", "cleanup")]
    [InlineData("генеральная уборка", "deep-clean")]
    [InlineData("полная очистка мусора", "deep-clean")]
    [InlineData("полная очистка мусора и кэша", "deep-clean")]
    [InlineData("полная очистка кэша", "amd-cache")]
    [InlineData("сделай, пожалуйста, генеральную уборку", "deep-clean")]
    [InlineData("не выключай компьютер", null)]
    [InlineData("выключи компьютер", "system")]
    [InlineData("открой хром", "browser")]
    public void Route_SelectsExpectedSkill(string input, string? expected)
    {
        Assert.Equal(expected, RouteName(input));
    }

    [Fact]
    public void Route_PrefersHigherPriorityTrigger()
    {
        var low = new FakeSkill("low", new SkillTrigger("очисти кэш", priority: 0));
        var high = new FakeSkill("high", new SkillTrigger("очисти кэш", priority: 50));

        RoutingResult result = new IntentRouter(new[] { low, high }).Route("очисти кэш");

        Assert.Equal("high", result.Best!.Skill.Name);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public void Route_RespectsExcludes()
    {
        var skill = new FakeSkill(
            "amd",
            new SkillTrigger("очисти кэш", excludes: new[] { "браузер*" }));

        var router = new IntentRouter(new[] { skill });

        Assert.Null(router.Route("очисти кэш браузера").Best);
        Assert.NotNull(router.Route("очисти кэш").Best);
    }

    [Fact]
    public void Route_RespectsRequiresAny()
    {
        var skill = new FakeSkill(
            "amd",
            new SkillTrigger("очисти кэш", requiresAny: new[] { "амд", "amd" }));

        var router = new IntentRouter(new[] { skill });

        Assert.Null(router.Route("очисти кэш").Best);
        Assert.NotNull(router.Route("очисти кэш амд").Best);
    }

    [Fact]
    public void Route_SkipsSkillThatCannotHandleCommand()
    {
        var skill = new FakeSkill(
            "calculator",
            new SkillTrigger("посчитай"),
            canHandle: _ => false);

        Assert.Null(new IntentRouter(new[] { skill }).Route("посчитай").Best);
    }

    [Fact]
    public void Route_ReportsAmbiguity()
    {
        var first = new FakeSkill("first", new SkillTrigger("сделай уборку"));
        var second = new FakeSkill("second", new SkillTrigger("сделай уборку"));

        RoutingResult result = new IntentRouter(new[] { first, second })
            .Route("сделай уборку");

        Assert.True(result.IsAmbiguous);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Route_DoesNotMatchSubstringsInsideWords()
    {
        var skill = new FakeSkill("sleep", new SkillTrigger("сон"));

        Assert.Null(new IntentRouter(new[] { skill }).Route("включи персонажа").Best);
    }

    [Fact]
    public void SystemSkill_CanHandleOnlyKnownActions()
    {
        var skill = new SystemSkill();

        Assert.True(skill.CanHandle("выключи компьютер"));
        Assert.True(skill.CanHandle("сделай громче"));
        Assert.False(skill.CanHandle("включи персонажа"));
        Assert.False(skill.CanHandle("не выключай компьютер"));
        Assert.False(skill.CanHandle("не выключи компьютер"));
    }

    [Fact]
    public void CalculatorSkill_CanHandleOnlyWhenExpressionExists()
    {
        var skill = new CalculatorSkill();

        Assert.True(skill.CanHandle("сколько будет 2 плюс 2"));
        Assert.False(skill.CanHandle("посчитай"));
        Assert.False(skill.CanHandle("сколько времени"));
    }

    [Fact]
    public void AmdCacheSkill_CanHandleOnlyAmdCacheCommands()
    {
        var skill = new AmdCacheSkill();

        Assert.True(skill.CanHandle("очисти кэш"));
        Assert.True(skill.CanHandle("очисти кэш амд"));
        Assert.False(skill.CanHandle("очисти кэш браузера"));
        Assert.False(skill.CanHandle("очисти кэш хрома"));
        Assert.False(skill.CanHandle("очисти мусор"));
    }

    private sealed class FakeSkill : ISkill
    {
        private readonly Func<string, bool>? _canHandle;

        public FakeSkill(
            string name,
            SkillTrigger trigger,
            Func<string, bool>? canHandle = null)
        {
            Name = name;
            Description = name;
            Triggers = new[] { trigger };
            _canHandle = canHandle;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<SkillTrigger> Triggers { get; }

        public bool CanHandle(string command) => _canHandle?.Invoke(command) ?? true;

        public Task<CommandResult> ExecuteAsync(string command)
            => Task.FromResult(CommandResult.Ok("ok"));
    }
}
