using System.Text.RegularExpressions;

namespace Fenrir.Core.Routing;

/// <summary>
/// Приводит команду к единому виду для сравнения: нижний регистр,
/// «ё» → «е», пунктуация и дефисы становятся пробелами.
/// </summary>
public static partial class TextNormalizer
{
    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string lowered = text.ToLowerInvariant().Replace('ё', 'е');
        return SeparatorRegex().Replace(lowered, " ").Trim();
    }

    public static IReadOnlyList<string> Tokenize(string? text)
    {
        string normalized = Normalize(text);

        return normalized.Length == 0
            ? Array.Empty<string>()
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
