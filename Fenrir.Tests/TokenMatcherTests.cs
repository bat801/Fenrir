using Fenrir.Core.Routing;

namespace Fenrir.Tests;

public sealed class TokenMatcherTests
{
    [Fact]
    public void MatchesToken_SupportsExactAndWildcardPatterns()
    {
        Assert.True(TokenMatcher.MatchesToken("время", "время"));
        Assert.True(TokenMatcher.MatchesToken("времени", "врем*"));
        Assert.False(TokenMatcher.MatchesToken("персона", "сон"));
        Assert.False(TokenMatcher.MatchesToken("часть", "час"));
    }

    [Fact]
    public void MatchSequence_AllowsGapsButNotReordering()
    {
        var input = TextNormalizer.Tokenize("сделай пожалуйста генеральную уборку");
        var pattern = new[] { "генеральн*", "уборк*" };

        var match = TokenMatcher.MatchSequence(input, pattern);

        Assert.NotNull(match);
        Assert.Equal(0, match!.GapCount);
        Assert.True(match.IsContiguous);
        Assert.False(match.IsExactPhrase);
    }

    [Fact]
    public void MatchSequence_RejectsWrongOrder()
    {
        var input = TextNormalizer.Tokenize("уборку генеральную");

        Assert.Null(TokenMatcher.MatchSequence(input, new[] { "генеральн*", "уборк*" }));
    }

    [Fact]
    public void MatchSequence_DetectsExactPhrase()
    {
        var input = TextNormalizer.Tokenize("очисти кэш");

        var match = TokenMatcher.MatchSequence(input, new[] { "очисти", "кэш" });

        Assert.NotNull(match);
        Assert.True(match!.IsExactPhrase);
    }

    [Fact]
    public void SkillTrigger_RejectsTooShortWildcardStem()
    {
        Assert.Throws<ArgumentException>(() => new SkillTrigger("со*"));
    }
}
