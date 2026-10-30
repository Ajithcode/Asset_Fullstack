using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetBulkAssignmentRepository
    : MongoRepository<AssetBulkAssignment>, IAssetBulkAssignmentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetBulkAssignment";

    public const string DraftStatus = "Draft";
    public const string CompletedStatus = "Completed";

    public AssetBulkAssignmentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetBulkAssignment>(CollectionName))
    {
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetBulkAssignment>(
                Builders<AssetBulkAssignment>.IndexKeys.Ascending(batch => batch.BatchNumber),
                new CreateIndexOptions { Name = "ix_asset_bulk_assignment_batch_number" }),
            new CreateIndexModel<AssetBulkAssignment>(
                Builders<AssetBulkAssignment>.IndexKeys
                    .Ascending(batch => batch.CreatedBy)
                    .Descending(batch => batch.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_bulk_assignment_created_by_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
