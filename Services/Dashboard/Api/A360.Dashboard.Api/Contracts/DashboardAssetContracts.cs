using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Contracts;

public sealed record DashboardAssetResponse(
    string Id,
    string? AssetId,
    string? AssetName,
    string? MappedUniqueId)
{
    public static DashboardAssetResponse FromAsset(DashboardAsset asset)
    {
        return new DashboardAssetResponse(asset.Id, asset.AssetId, asset.AssetName, asset.MappedUniqueId);
    }
}

public sealed record DashboardAssetListResponse(
    int TotalCount,
    IReadOnlyCollection<DashboardAssetResponse> Data)
{
    public static DashboardAssetListResponse FromAssets(IReadOnlyCollection<DashboardAsset> assets)
    {
        return new DashboardAssetListResponse(
            assets.Count,
            assets.Select(DashboardAssetResponse.FromAsset).ToList());
    }
}
