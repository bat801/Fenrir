using Fenrir.Core.Skills;

namespace Fenrir.Core.Routing;

public sealed record SkillMatch(
    ISkill Skill,
    SkillTrigger Trigger,
    int Score,
    TokenMatch Match);

public sealed record RoutingResult(
    SkillMatch? Best,
    IReadOnlyList<SkillMatch> Candidates,
    bool IsAmbiguous)
{
    public static RoutingResult Empty { get; } =
        new(null, Array.Empty<SkillMatch>(), false);
}

/// <summary>
/// Детерминированный двухступенчатый роутер:
/// сначала ищет кандидатов по токенам, затем проверяет, умеет ли навык
/// обработать команду. Не запускает процессы и не трогает файловую систему.
/// </summary>
public sealed class IntentRouter
{
    private const int MinimumScore = 100;

    private readonly IReadOnlyList<ISkill> _skills;

    public IntentRouter(IEnumerable<ISkill> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _skills = skills.ToArray();
    }

    public RoutingResult Route(string command)
    {
        string normalized = TextNormalizer.Normalize(command);

        if (normalized.Length == 0)
            return RoutingResult.Empty;

        IReadOnlyList<string> tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        var matches = new List<SkillMatch>();

        foreach (ISkill skill in _skills)
        {
            SkillMatch? best = null;

            foreach (SkillTrigger trigger in skill.Triggers)
            {
                if (IsExcluded(tokens, trigger) ||
                    !SatisfiesRequiresAny(tokens, trigger))
                {
                    continue;
                }

                TokenMatch? match = TokenMatcher.MatchSequence(tokens, trigger.Tokens);
                if (match is null)
                    continue;

                int score = trigger.Tokens.Count * 100
                            + trigger.Priority
                            + (match.IsExactPhrase ? 50 : 0)
                            - match.GapCount * 10;

                if (score < MinimumScore)
                    continue;

                var candidate = new SkillMatch(skill, trigger, score, match);
                if (best is null || IsBetter(candidate, best))
                    best = candidate;
            }

            if (best is not null && skill.CanHandle(command))
                matches.Add(best);
        }

        if (matches.Count == 0)
            return RoutingResult.Empty;

        SkillMatch[] ordered = matches
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Trigger.Priority)
            .ThenByDescending(match => match.Trigger.Tokens.Count)
            .ThenBy(match => match.Skill.Name, StringComparer.Ordinal)
            .ToArray();

        bool isAmbiguous = ordered.Length > 1 &&
                           ordered[0].Score == ordered[1].Score;

        return new RoutingResult(ordered[0], ordered, isAmbiguous);
    }

    private static bool IsExcluded(
        IReadOnlyList<string> tokens,
        SkillTrigger trigger)
    {
        return trigger.Excludes.Any(pattern =>
            tokens.Any(token => TokenMatcher.MatchesToken(token, pattern)));
    }

    private static bool SatisfiesRequiresAny(
        IReadOnlyList<string> tokens,
        SkillTrigger trigger)
    {
        if (trigger.RequiresAny.Count == 0)
            return true;

        return trigger.RequiresAny.Any(pattern =>
            tokens.Any(token => TokenMatcher.MatchesToken(token, pattern)));
    }

    private static bool IsBetter(SkillMatch candidate, SkillMatch current)
    {
        if (candidate.Score != current.Score)
            return candidate.Score > current.Score;

        if (candidate.Trigger.Priority != current.Trigger.Priority)
            return candidate.Trigger.Priority > current.Trigger.Priority;

        if (candidate.Trigger.Tokens.Count != current.Trigger.Tokens.Count)
            return candidate.Trigger.Tokens.Count > current.Trigger.Tokens.Count;

        return string.CompareOrdinal(candidate.Skill.Name, current.Skill.Name) < 0;
    }
}
