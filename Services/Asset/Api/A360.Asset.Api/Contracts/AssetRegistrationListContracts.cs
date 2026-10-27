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
