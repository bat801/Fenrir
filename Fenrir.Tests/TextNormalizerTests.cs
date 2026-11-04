using Fenrir.Core.Routing;

namespace Fenrir.Tests;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Сделай, пожалуйста, ГЕНЕРАЛЬНУЮ уборку!", "сделай пожалуйста генеральную уборку")]
    [InlineData("  Ёлка   и   ёжик  ", "елка и ежик")]
    [InlineData("AMD-кэш", "amd кэш")]
    [InlineData("2+2=?", "2 2")]
    public void Normalize_ProducesComparableText(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Tokenize_SplitsWordsAndNumbers()
    {
        Assert.Equal(
            new[] { "очисти", "кэш", "amd" },
            TextNormalizer.Tokenize("Очисти кэш AMD!"));
    }
}
