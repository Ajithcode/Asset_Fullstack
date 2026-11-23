using CriticalityEntity = A360.MasterDataManagement.Domain.Entities.Criticality;

namespace A360.MasterDataManagement.Api.Validation;

/// <summary>Score ranges of the Criticality levels: whole numbers, no overlap, and (when saved together) no gap from 20 to 100.</summary>
public static class CriticalityRanges
{
    public const int Floor = 20;
    public const int Ceiling = 100;

    /// <summary>Default ranges by level name; used to fill existing levels and for the seed.</summary>
    public static readonly IReadOnlyDictionary<string, (int Min, int Max, int Order)> Defaults =
        new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Critical"] = (85, 100, 1),
            ["High"] = (65, 84, 2),
            ["Medium"] = (40, 64, 3),
            ["Low"] = (20, 39, 4)
        };

    public static bool IsActive(CriticalityEntity level) =>
        !level.IsDeleted && !string.Equals(level.Status, "Inactive", StringComparison.OrdinalIgnoreCase);

    /// <summary>Errors for the active levels that have a range. <paramref name="requireFullCoverage"/> also rejects gaps between 20 and 100.</summary>
    public static List<string> Check(IEnumerable<CriticalityEntity> levels, bool requireFullCoverage)
    {
        var errors = new List<string>();
        var ranged = levels
            .Where(level => IsActive(level) && level.MinScore is not null && level.MaxScore is not null)
            .OrderBy(level => level.MinScore)
            .ThenBy(level => level.MaxScore)
            .ToList();

        foreach (var level in ranged)
        {
            if (level.MinScore < 0 || level.MaxScore > Ceiling)
            {
                errors.Add($"{level.CriticalityName}: scores must be between 0 and {Ceiling}.");
            }
            else if (level.MinScore > level.MaxScore)
            {
                errors.Add($"{level.CriticalityName}: the minimum score ({level.MinScore}) is above the maximum ({level.MaxScore}).");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        for (var i = 1; i < ranged.Count; i++)
        {
            var previous = ranged[i - 1];
            var current = ranged[i];
            if (current.MinScore <= previous.MaxScore)
            {
                errors.Add($"{previous.CriticalityName} ({previous.MinScore}-{previous.MaxScore}) overlaps {current.CriticalityName} ({current.MinScore}-{current.MaxScore}).");
            }
            else if (requireFullCoverage && current.MinScore > previous.MaxScore + 1)
            {
                errors.Add($"Scores {previous.MaxScore + 1}-{current.MinScore - 1} between {previous.CriticalityName} and {current.CriticalityName} belong to no level.");
            }
        }

        if (requireFullCoverage)
        {
            if (ranged.Count == 0)
            {
                errors.Add($"Define the score ranges so that they cover {Floor}-{Ceiling}.");
            }
            else
            {
                if (ranged[0].MinScore > Floor)
                {
                    errors.Add($"Scores {Floor}-{ranged[0].MinScore - 1} below {ranged[0].CriticalityName} belong to no level.");
                }

                if (ranged[^1].MaxScore < Ceiling)
                {
                    errors.Add($"Scores {ranged[^1].MaxScore + 1}-{Ceiling} above {ranged[^1].CriticalityName} belong to no level.");
                }
            }
        }

        return errors;
    }
}
