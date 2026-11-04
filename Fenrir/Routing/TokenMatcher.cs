namespace Fenrir.Core.Routing;

/// <summary>
/// Сравнивает нормализованные токены команды с токенами триггера.
/// Работает со словами, а не с подстроками всей строки.
/// </summary>
public static class TokenMatcher
{
    public static bool MatchesToken(string token, string pattern)
    {
        if (pattern.EndsWith('*'))
        {
            string stem = pattern[..^1];
            return stem.Length > 0 && token.StartsWith(stem, StringComparison.Ordinal);
        }

        return string.Equals(token, pattern, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ищет токены триггера в команде в том же порядке. Вставки между
    /// словами разрешены, но увеличивают штраф через <see cref="TokenMatch.GapCount"/>.
    /// </summary>
    public static TokenMatch? MatchSequence(
        IReadOnlyList<string> input,
        IReadOnlyList<string> pattern)
    {
        if (pattern.Count == 0 || input.Count < pattern.Count)
            return null;

        TokenMatch? best = null;

        for (int start = 0; start <= input.Count - pattern.Count; start++)
        {
            if (!MatchesToken(input[start], pattern[0]))
                continue;

            var indices = new int[pattern.Count];
            indices[0] = start;

            int cursor = start + 1;
            bool matched = true;

            for (int i = 1; i < pattern.Count; i++)
            {
                while (cursor < input.Count && !MatchesToken(input[cursor], pattern[i]))
                    cursor++;

                if (cursor >= input.Count)
                {
                    matched = false;
                    break;
                }

                indices[i] = cursor;
                cursor++;
            }

            if (!matched)
                continue;

            int gaps = 0;
            for (int i = 1; i < indices.Length; i++)
                gaps += indices[i] - indices[i - 1] - 1;

            var candidate = new TokenMatch(
                indices,
                gaps,
                gaps == 0,
                input.Count == pattern.Count && indices[0] == 0);

            if (best is null || IsBetter(candidate, best))
                best = candidate;
        }

        return best;
    }

    public static bool Contains(IReadOnlyList<string> input, params string[] pattern)
        => MatchSequence(input, pattern) is not null;

    public static bool ContainsAny(IReadOnlyList<string> input, params string[] patterns)
        => patterns.Any(pattern => input.Any(token => MatchesToken(token, pattern)));

    private static bool IsBetter(TokenMatch candidate, TokenMatch current)
    {
        if (candidate.GapCount != current.GapCount)
            return candidate.GapCount < current.GapCount;

        return candidate.Indices[0] < current.Indices[0];
    }
}

public sealed record TokenMatch(
    IReadOnlyList<int> Indices,
    int GapCount,
    bool IsContiguous,
    bool IsExactPhrase);
