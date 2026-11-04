namespace Fenrir.Core.Routing;

/// <summary>
/// Описание фразы, по которой навык может быть выбран.
/// Поддерживает суффиксный wildcard для русской морфологии: «уборк*».
/// </summary>
public sealed class SkillTrigger
{
    private const int MinimumWildcardStemLength = 3;

    public SkillTrigger(
        string phrase,
        int priority = 0,
        IEnumerable<string>? excludes = null,
        IEnumerable<string>? requiresAny = null)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            throw new ArgumentException("Триггер не может быть пустым.", nameof(phrase));

        DisplayPhrase = phrase.Trim();
        Tokens = ParseTokens(phrase);

        if (Tokens.Count == 0)
            throw new ArgumentException("Триггер не содержит слов.", nameof(phrase));

        Phrase = string.Join(" ", Tokens);
        Priority = priority;
        Excludes = ParsePatterns(excludes, nameof(excludes));
        RequiresAny = ParsePatterns(requiresAny, nameof(requiresAny));
    }

    /// <summary>Нормализованная фраза с wildcard-шаблонами.</summary>
    public string Phrase { get; }

    /// <summary>Исходная фраза для подсказок пользователю.</summary>
    public string DisplayPhrase { get; }

    public int Priority { get; }

    public IReadOnlyList<string> Tokens { get; }

    /// <summary>Если в команде есть любой из этих токенов, триггер не подходит.</summary>
    public IReadOnlyList<string> Excludes { get; }

    /// <summary>Если список не пуст, в команде должен быть хотя бы один из токенов.</summary>
    public IReadOnlyList<string> RequiresAny { get; }

    private static IReadOnlyList<string> ParseTokens(string phrase)
    {
        var tokens = new List<string>();

        foreach (string raw in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool wildcard = raw.EndsWith('*');
            string body = wildcard ? raw[..^1] : raw;
            string normalized = TextNormalizer.Normalize(body);

            if (normalized.Length == 0)
                continue;

            // Дефис внутри слова нормализуется в пробел: «amd-кэш» → «amd», «кэш».
            if (normalized.Contains(' '))
            {
                tokens.AddRange(normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                continue;
            }

            if (!wildcard)
            {
                tokens.Add(normalized);
                continue;
            }

            if (normalized.Length < MinimumWildcardStemLength)
            {
                throw new ArgumentException(
                    $"Слишком короткий шаблон с '*': {raw}",
                    nameof(phrase));
            }

            tokens.Add(normalized + "*");
        }

        return tokens.ToArray();
    }

    private static IReadOnlyList<string> ParsePatterns(
        IEnumerable<string>? patterns,
        string parameterName)
    {
        if (patterns is null)
            return Array.Empty<string>();

        var result = new List<string>();

        foreach (string pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            string raw = pattern.Trim();
            bool wildcard = raw.EndsWith('*');
            string body = wildcard ? raw[..^1] : raw;
            string normalized = TextNormalizer.Normalize(body);

            if (normalized.Length == 0)
                continue;

            if (normalized.Contains(' '))
            {
                throw new ArgumentException(
                    $"Шаблон должен быть одним словом: {pattern}",
                    parameterName);
            }

            if (!wildcard)
            {
                result.Add(normalized);
                continue;
            }

            if (normalized.Length < MinimumWildcardStemLength)
            {
                throw new ArgumentException(
                    $"Слишком короткий шаблон с '*': {pattern}",
                    parameterName);
            }

            result.Add(normalized + "*");
        }

        return result.ToArray();
    }
}
