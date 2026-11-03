namespace A360.Dashboard.Repository.Repositories;

public interface IDashboardAssetRepository
{
    Task<IReadOnlyCollection<DashboardAsset>> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record DashboardAsset(string Id, string? AssetId, string? AssetName, string? MappedUniqueId);
