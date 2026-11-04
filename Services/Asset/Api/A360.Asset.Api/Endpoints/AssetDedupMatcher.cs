using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Duplicate detection for Asset Merge / Dedup: a weighted similarity score between two assets, and the grouping
/// of assets that score above the Low threshold. Same rules as the screen's original client-side matcher
/// (projects/assets/src/app/asset-merge/dedup-match.ts), so scores do not change with the move to the API.
/// </summary>
public static class AssetDedupMatcher
{
    public const string RegisterSource = "Asset List";

    /// <summary>Weight of each comparable field.</summary>
    private static readonly Dictionary<string, int> Weights = new()
    {
        ["name"] = 35,
        ["serialNumber"] = 40,
        ["model"] = 25,
        ["manufacturer"] = 10,
        ["category"] = 10,
        ["location"] = 10
    };

    public static AssetDedupRules DefaultRules() => new()
    {
        Fields = ["name", "serialNumber", "model"],
        High = 90,
        Medium = 70,
        Low = 50,
        IncludeInactive = true
    };

    /// <summary>An asset with the values the screen compares and merges, and the registration it came from.</summary>
    public sealed record Candidate(AssetDedupRecordResponse Record, AssetRegistration Asset);

    public sealed record Member(Candidate Candidate, int Score);

    public sealed record Group(Candidate Primary, List<Member> Members)
    {
        public int MaxScore => Members.Count == 0 ? 0 : Members.Max(member => member.Score);
    }

    public static Candidate ToCandidate(AssetRegistration asset)
    {
        var basic = asset.BasicInformation;
        var purchase = asset.PurchaseWarranty.Purchase;
        var location = asset.LocationAssignment.Location;
        var modified = asset.UpdatedAt ?? asset.CreatedAt;

        var record = new AssetDedupRecordResponse(
            Tag: Text(basic.AssetTag) ?? asset.AssetRegistrationId,
            AssetRegistrationId: asset.AssetRegistrationId,
            Name: basic.AssetName ?? string.Empty,
            Type: asset.Classification.AssetType ?? string.Empty,
            Manufacturer: Text(basic.Manufacturer) ?? purchase.Manufacturer ?? string.Empty,
            Model: basic.Model ?? string.Empty,
            SerialNumber: basic.SerialNumber ?? string.Empty,
            Category: asset.Classification.Category ?? string.Empty,
            Location: Text(location.LocationPath) ?? string.Join(" - ", new[] { location.SiteName, location.BuildingName, location.FloorName, location.ZoneName }.Where(part => !string.IsNullOrWhiteSpace(part))),
            Status: Text(basic.Status) ?? asset.Status ?? string.Empty,
            PurchaseDate: purchase.PurchaseDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            Supplier: purchase.SupplierVendor ?? string.Empty,
            Value: purchase.PurchaseCost ?? 0,
            LastModified: modified?.ToString("yyyy-MM-ddTHH:mm") ?? string.Empty,
            ModifiedBy: asset.UpdatedBy ?? asset.CreatedBy ?? string.Empty,
            Source: RegisterSource,
            InRegister: true);

        return new Candidate(record, asset);
    }

    public static string PairKey(string a, string b)
    {
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    public static string? Confidence(int score, AssetDedupRules rules)
    {
        return score >= rules.High ? "High" : score >= rules.Medium ? "Medium" : score >= rules.Low ? "Low" : null;
    }

    /// <summary>
    /// Groups possible duplicates among the register assets. The older asset (registered first) is the primary;
    /// assets are never chained to each other through a third record, and dismissed pairs never match.
    /// </summary>
    public static List<Group> FindGroups(IEnumerable<AssetRegistration> assets, AssetDedupRules rules, ISet<string> dismissedPairs)
    {
        var register = assets
            .Where(asset => !asset.IsDeleted)
            .OrderBy(asset => asset.CreatedAt ?? DateTime.MinValue)
            .ThenBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(ToCandidate)
            .Where(candidate => rules.IncludeInactive || !string.Equals(candidate.Record.Status, "Disposed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < register.Count; i++)
        {
            for (var j = i + 1; j < register.Count; j++)
            {
                var a = register[i];
                var b = register[j];
                if (members.Contains(a.Record.Tag) || members.Contains(b.Record.Tag) || groups.ContainsKey(b.Record.Tag))
                {
                    continue;
                }

                var score = dismissedPairs.Contains(PairKey(a.Record.Tag, b.Record.Tag)) ? 0 : MatchScore(a.Record, b.Record, rules);
                if (score < rules.Low)
                {
                    continue;
                }

                if (!groups.TryGetValue(a.Record.Tag, out var group))
                {
                    group = new Group(a, []);
                    groups[a.Record.Tag] = group;
                }

                group.Members.Add(new Member(b, score));
                members.Add(b.Record.Tag);
            }
        }

        foreach (var group in groups.Values)
        {
            group.Members.Sort((x, y) => y.Score.CompareTo(x.Score));
        }

        return groups.Values.OrderByDescending(group => group.MaxScore).ToList();
    }

    /// <summary>Match score 0-100. Serial numbers must match exactly; an empty serial counts as unknown (half weight).</summary>
    public static int MatchScore(AssetDedupRecordResponse a, AssetDedupRecordResponse b, AssetDedupRules rules)
    {
        double total = 0;
        double got = 0;
        foreach (var (field, weight) in Weights)
        {
            if (!rules.Fields.Contains(field))
            {
                continue;
            }

            total += weight;
            var x = Value(a, field);
            var y = Value(b, field);
            var similarity = field == "serialNumber"
                ? (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y) ? 0.5 : Squash(x) == Squash(y) ? 1 : 0)
                : Similarity(x, y);
            got += weight * similarity;
        }

        return total == 0 ? 0 : (int)Math.Round(got / total * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>Sørensen–Dice coefficient on letter pairs (0-1).</summary>
    public static double Similarity(string a, string b)
    {
        var x = Squash(a);
        var y = Squash(b);
        if (x.Length == 0 || y.Length == 0)
        {
            return 0;
        }

        if (x == y)
        {
            return 1;
        }

        if (x.Length < 2 || y.Length < 2)
        {
            return 0;
        }

        var pairs = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < x.Length - 1; i++)
        {
            var pair = x.Substring(i, 2);
            pairs[pair] = pairs.GetValueOrDefault(pair) + 1;
        }

        var hits = 0;
        for (var i = 0; i < y.Length - 1; i++)
        {
            var pair = y.Substring(i, 2);
            if (pairs.TryGetValue(pair, out var count) && count > 0)
            {
                hits++;
                pairs[pair] = count - 1;
            }
        }

        return 2.0 * hits / (x.Length - 1 + y.Length - 1);
    }

    /// <summary>Lower case letters and digits only, so "250KVA" and "250 kVA" compare equal.</summary>
    private static string Squash(string value)
    {
        return new string(value.ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
    }

    private static string Value(AssetDedupRecordResponse record, string field) => field switch
    {
        "name" => record.Name,
        "serialNumber" => record.SerialNumber,
        "model" => record.Model,
        "manufacturer" => record.Manufacturer,
        "category" => record.Category,
        "location" => record.Location,
        _ => string.Empty
    };

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
