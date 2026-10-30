using MongoDB.Driver;
using A360.Repository.Repositories;
using A360.ReportAnalytics.Domain.Entities;

namespace A360.ReportAnalytics.Repository.Repositories;

public interface IAssetPerformanceViewRepository : IMongoRepository<AssetPerformanceView>
{
    Task<AssetPerformanceView?> GetByViewIdAsync(string viewId, CancellationToken cancellationToken = default);

    /// <summary>The owner's views in the tenant, default first then by name.</summary>
    Task<IReadOnlyList<AssetPerformanceView>> GetOwnedAsync(string? ownerId, string? tenantId, string? clientId, CancellationToken cancellationToken = default);

    /// <summary>Clears the default flag on the owner's other views.</summary>
    Task ClearDefaultAsync(string? ownerId, string? tenantId, string? clientId, string exceptId, CancellationToken cancellationToken = default);
}

public sealed class AssetPerformanceViewRepository
    : MongoRepository<AssetPerformanceView>, IAssetPerformanceViewRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetPerformanceView";

    public AssetPerformanceViewRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetPerformanceView>(CollectionName))
    {
    }

    public async Task<AssetPerformanceView?> GetByViewIdAsync(string viewId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(viewId))
        {
            return null;
        }

        return await Collection
            .Find(view => view.ViewId == viewId.Trim() && !view.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetPerformanceView>> GetOwnedAsync(
        string? ownerId,
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(OwnedFilter(ownerId, tenantId, clientId))
            .Sort(Builders<AssetPerformanceView>.Sort.Descending(view => view.IsDefault).Ascending(view => view.ViewName))
            .ToListAsync(cancellationToken);
    }

    public async Task ClearDefaultAsync(string? ownerId, string? tenantId, string? clientId, string exceptId, CancellationToken cancellationToken = default)
    {
        var filter = OwnedFilter(ownerId, tenantId, clientId) & Builders<AssetPerformanceView>.Filter.Ne(view => view.Id, exceptId);
        await Collection.UpdateManyAsync(filter, Builders<AssetPerformanceView>.Update.Set(view => view.IsDefault, false), cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetPerformanceView>(
                Builders<AssetPerformanceView>.IndexKeys.Ascending(view => view.ViewId),
                new CreateIndexOptions { Name = "ux_asset_performance_view_view_id", Unique = true }),
            new CreateIndexModel<AssetPerformanceView>(
                Builders<AssetPerformanceView>.IndexKeys
                    .Ascending(view => view.TenantId)
                    .Ascending(view => view.OwnerId)
                    .Ascending(view => view.ViewName),
                new CreateIndexOptions { Name = "ix_asset_performance_view_tenant_id_owner_id_view_name" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static FilterDefinition<AssetPerformanceView> OwnedFilter(string? ownerId, string? tenantId, string? clientId)
    {
        var builder = Builders<AssetPerformanceView>.Filter;
        var filter = builder.Eq(view => view.IsDeleted, false) & builder.Eq(view => view.OwnerId, ownerId);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(view => view.TenantId, tenantId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(view => view.ClientId, clientId.Trim());
        }

        return filter;
    }
}
