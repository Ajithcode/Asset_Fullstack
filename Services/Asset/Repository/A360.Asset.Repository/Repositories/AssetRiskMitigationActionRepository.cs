using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetRiskMitigationActionRepository
    : MongoRepository<AssetRiskMitigationAction>, IAssetRiskMitigationActionRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetRiskMitigationAction";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    // Statuses that are no longer open.
    private static readonly string[] ClosedStatuses = ["Completed", "Cancelled"];

    public AssetRiskMitigationActionRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetRiskMitigationAction>(CollectionName))
    {
    }

    public async Task<AssetRiskMitigationActionQueryResult> QueryAsync(AssetRiskMitigationActionQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetRiskMitigationAction>>
        {
            Builders<AssetRiskMitigationAction>.Filter.Eq(action => action.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            filters.Add(Builders<AssetRiskMitigationAction>.Filter.Eq(action => action.AssetRegistrationId, query.AssetRegistrationId.Trim()));
        }

        // The open count ignores the status filter, so the header stays the same while filtering.
        var openFilter = Builders<AssetRiskMitigationAction>.Filter.And(
            filters.Append(Builders<AssetRiskMitigationAction>.Filter.Nin(action => action.Status, ClosedStatuses)));

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(Builders<AssetRiskMitigationAction>.Filter.Eq(action => action.Status, query.Status.Trim()));
        }

        var filter = Builders<AssetRiskMitigationAction>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetRiskMitigationAction>.Sort.Ascending(action => action.CreatedAt).Ascending(action => action.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var openCount = await Collection.CountDocumentsAsync(openFilter, cancellationToken: cancellationToken);

        return new AssetRiskMitigationActionQueryResult(items, totalCount, openCount);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetRiskMitigationAction>(
                Builders<AssetRiskMitigationAction>.IndexKeys
                    .Ascending(action => action.AssetRegistrationId)
                    .Ascending(action => action.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_risk_mitigation_action_asset_registration_id_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
