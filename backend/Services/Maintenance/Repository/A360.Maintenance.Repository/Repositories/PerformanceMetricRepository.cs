
using PerformanceMetricEntity = A360.Maintenance.Domain.Entities.PerformanceMetric;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class PerformanceMetricRepository : MongoRepository<PerformanceMetricEntity>,
    IPerformanceMetricRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "performance_metrics";

    public PerformanceMetricRepository(IMongoDatabase database)
        : base(database.GetCollection<PerformanceMetricEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PerformanceMetricEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PerformanceMetricEntity>(
                Builders<PerformanceMetricEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_performance_metric_client"
                }),
            new CreateIndexModel<PerformanceMetricEntity>(
                Builders<PerformanceMetricEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_performance_metric_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
