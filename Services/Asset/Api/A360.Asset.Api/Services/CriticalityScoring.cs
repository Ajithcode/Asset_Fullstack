using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Services;

/// <summary>
/// Scoring, done only here (the client's score and weights are never used): score = sum(rating / 5 x weight), one
/// decimal; the level comes from the whole part of the score against the MDM level ranges (84.5 is High, not Critical).
/// </summary>
public static class CriticalityScoring
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public sealed record ScoredCriterion(AssetCriticalityTemplateCriterion Criterion, int Rating, decimal Weighted);

    public sealed record ScoreResult(decimal Score, CriticalityLevelInfo? Level, IReadOnlyList<ScoredCriterion> Breakdown);

    /// <summary>Scores integer ratings (by criterion code) against a template version. Null when a rating is missing or not 1-5.</summary>
    public static ScoreResult? Score(
        AssetCriticalityTemplate template,
        IReadOnlyDictionary<string, int> ratingsByCode,
        IReadOnlyList<CriticalityLevelInfo> levels)
    {
        var breakdown = new List<ScoredCriterion>();
        foreach (var criterion in template.Criteria)
        {
            if (!ratingsByCode.TryGetValue(criterion.Code, out var rating) || rating is < MinRating or > MaxRating)
            {
                return null;
            }

            breakdown.Add(new ScoredCriterion(criterion, rating, Math.Round((decimal)criterion.Weight * rating / MaxRating, 1, MidpointRounding.AwayFromZero)));
        }

        // Round the sum of the exact values once (not the sum of rounded rows).
        var exact = template.Criteria.Sum(criterion => (decimal)criterion.Weight * ratingsByCode[criterion.Code] / MaxRating);
        var score = Math.Round(exact, 1, MidpointRounding.AwayFromZero);
        return new ScoreResult(score, LevelForScore(score, levels), breakdown);
    }

    /// <summary>The level whose min-max range holds the whole part of the score; the lowest level below every range.</summary>
    public static CriticalityLevelInfo? LevelForScore(decimal score, IReadOnlyList<CriticalityLevelInfo> levels)
    {
        var whole = (int)Math.Floor(score);
        var ranged = levels.Where(level => level.MinScore is not null && level.MaxScore is not null).ToList();
        return ranged.FirstOrDefault(level => whole >= level.MinScore && whole <= level.MaxScore)
            ?? ranged.OrderBy(level => level.MinScore).FirstOrDefault(level => whole < level.MinScore)
            ?? ranged.OrderByDescending(level => level.MaxScore).FirstOrDefault();
    }

    /// <summary>Approval rule that applies: lowered level, unchanged level on a reassessment, else the proposed level.</summary>
    public static string Scenario(string level, string? previousLevel)
    {
        if (!string.IsNullOrWhiteSpace(previousLevel))
        {
            if (Security.CriticalityAccess.IsReduction(previousLevel, level))
            {
                return "LevelReduced";
            }

            if (string.Equals(previousLevel, level, StringComparison.OrdinalIgnoreCase))
            {
                return "ReviewNoChange";
            }
        }

        return "Level:" + level;
    }
}
