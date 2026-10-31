using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Contracts;

/// <summary>One row of the asset list; the full record comes from GET /api/asset-registrations/{assetRegistrationId}.</summary>
public sealed record AssetListItemResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string? ImageUrl,
    string? Category,
    string? AssetType,
    string? SiteName,
    string? BuildingName,
    string? FloorName,
    string? ZoneName,
    string? RoomName,
    string? LocationPath,
    string? CustodianUserId,
    string? CustodianName,
    string? Department,
    string? Status,
    string? Criticality,
    string? Manufacturer,
    DateTime? NextMaintenanceDate,
    DateTime? CreatedAt)
{
    public static AssetListItemResponse FromEntity(AssetRegistration asset)
    {
        var location = asset.LocationAssignment.Location;
        var assignment = asset.LocationAssignment.Assignment;

        return new AssetListItemResponse(
            asset.Id,
            asset.AssetRegistrationId,
            asset.BasicInformation.AssetName,
            asset.BasicInformation.AssetTag,
            asset.BasicInformation.Images.FirstOrDefault(),
            asset.Classification.Category,
            asset.Classification.AssetType,
            location.SiteName,
            location.BuildingName,
            location.FloorName,
            location.ZoneName,
            location.RoomName,
            location.LocationPath,
            assignment.AssignedToUserId,
            assignment.AssignedToName,
            asset.Classification.Department,
            asset.BasicInformation.Status,
            asset.Classification.Criticality,
            asset.PurchaseWarranty.Purchase.Manufacturer,
            asset.AdditionalInformation.Operational.NextMaintenanceDate,
            asset.CreatedAt);
    }
}

/// <summary>The status cards above the list. Statuses other than the four named ones are counted in Other.</summary>
public sealed record AssetStatusSummary(
    long Total,
    long Active,
    long UnderMaintenance,
    long InRepair,
    long Disposed,
    long Other)
{
    public static AssetStatusSummary FromCounts(IReadOnlyDictionary<string, long> statusCounts)
    {
        long CountOf(string status) => statusCounts
            .Where(entry => string.Equals(entry.Key, status, StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Value);

        var total = statusCounts.Values.Sum();
        var active = CountOf("Active");
        var underMaintenance = CountOf("Under Maintenance");
        var inRepair = CountOf("In Repair");
        var disposed = CountOf("Disposed");

        return new AssetStatusSummary(
            total,
            active,
            underMaintenance,
            inRepair,
            disposed,
            total - active - underMaintenance - inRepair - disposed);
    }
}

public sealed record AssetRegistrationListResponse(
    IReadOnlyCollection<AssetListItemResponse> Items,
    long TotalCount,
    int Page,
    int PageSize,
    AssetStatusSummary Summary)
{
    public static AssetRegistrationListResponse FromResult(AssetRegistrationQueryResult result, int page, int pageSize)
    {
        return new AssetRegistrationListResponse(
            result.Items.Select(AssetListItemResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize,
            AssetStatusSummary.FromCounts(result.StatusCounts));
    }
}

/// <summary>
/// The asset list filter dropdowns. Values are the ones used by registered assets, matched exactly by the list
/// filters, so every choice finds at least one asset (Status also lists every standard status).
/// </summary>
public sealed record AssetListFilterOptionsResponse(
    IReadOnlyList<string> Categories,
    IReadOnlyList<AssetTypeFilterOption> AssetTypes,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<AssetLocationFilterOption> Locations,
    IReadOnlyList<string> Departments,
    IReadOnlyList<AssetCustodianFilterOption> Custodians,
    IReadOnlyList<string> Criticalities,
    IReadOnlyList<string> Manufacturers,
    IReadOnlyList<string> Conditions)
{
    private static readonly string[] CriticalityRanks = ["Low", "Medium", "High", "Critical"];

    public static AssetListFilterOptionsResponse FromAssets(IReadOnlyCollection<AssetRegistration> assets)
    {
        var assetTypes = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Classification.AssetType))
            .Select(asset => new AssetTypeFilterOption(asset.Classification.AssetType!, asset.Classification.Category))
            .Distinct()
            .OrderBy(option => option.AssetType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var custodians = assets
            .Select(asset => asset.LocationAssignment.Assignment)
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.AssignedToUserId))
            .GroupBy(assignment => assignment.AssignedToUserId!)
            .Select(group => new AssetCustodianFilterOption(group.Key, group.Select(assignment => assignment.AssignedToName).FirstOrDefault(name => name is not null)))
            .OrderBy(option => option.Name ?? option.UserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var criticalities = Distinct(assets.Select(asset => asset.Classification.Criticality))
            .OrderBy(level => Array.FindIndex(CriticalityRanks, rank => string.Equals(rank, level, StringComparison.OrdinalIgnoreCase)) is var index and >= 0 ? index : CriticalityRanks.Length)
            .ThenBy(level => level, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Standard statuses first, in their usual order, then any other status found on an asset.
        var statuses = AssetRegistrationOptions.Statuses.All
            .Concat(Distinct(assets.Select(asset => asset.BasicInformation.Status)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AssetListFilterOptionsResponse(
            Distinct(assets.Select(asset => asset.Classification.Category)),
            assetTypes,
            statuses,
            AssetLocationFilterOption.FromAssets(assets),
            Distinct(assets.Select(asset => asset.Classification.Department)),
            custodians,
            criticalities,
            Distinct(assets.Select(asset => asset.PurchaseWarranty.Purchase.Manufacturer)),
            Distinct(assets.Select(asset => asset.BasicInformation.Condition)));
    }

    private static List<string> Distinct(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>An asset type with its category, so the Asset Type dropdown can narrow to the selected category.</summary>
public sealed record AssetTypeFilterOption(string AssetType, string? Category);

public sealed record AssetCustodianFilterOption(string UserId, string? Name);

/// <summary>
/// One level of the location hierarchy used by an asset; send Id as the list's locationId to match every asset
/// at or below it.
/// </summary>
public sealed record AssetLocationFilterOption(string Id, string Name, string Level, string Path)
{
    public static List<AssetLocationFilterOption> FromAssets(IEnumerable<AssetRegistration> assets)
    {
        var options = new Dictionary<string, AssetLocationFilterOption>();

        foreach (var location in assets.Select(asset => asset.LocationAssignment.Location))
        {
            var levels = new (string Level, string? Id, string? Name)[]
            {
                ("Country", location.CountryId, location.CountryName),
                ("Site", location.SiteId, location.SiteName),
                ("Building", location.BuildingId, location.BuildingName),
                ("Floor", location.FloorId, location.FloorName),
                ("Zone", location.ZoneId, location.ZoneName),
                ("Room", location.RoomId, location.RoomName)
            };

            var pathParts = new List<string>();
            foreach (var (level, id, name) in levels)
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                pathParts.Add(name);
                options.TryAdd(id, new AssetLocationFilterOption(id, name, level, string.Join(" > ", pathParts)));
            }
        }

        return options.Values
            .OrderBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
