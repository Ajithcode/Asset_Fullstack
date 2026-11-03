
using MaterialConsumptionEntity = A360.Wip.Domain.Entities.MaterialConsumption;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class MaterialConsumptionRepository : MongoRepository<MaterialConsumptionEntity>,
    IMaterialConsumptionRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "material_consumptions";

    public MaterialConsumptionRepository(IMongoDatabase database)
        : base(database.GetCollection<MaterialConsumptionEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MaterialConsumptionEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaterialConsumptionEntity>> GetByJobIdAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.JobId == jobId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MaterialConsumptionEntity>(
                Builders<MaterialConsumptionEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.MaterialId),
                new CreateIndexOptions
                {
                    Name = "ix_material_consumption_client_reference"
                }),

            new CreateIndexModel<MaterialConsumptionEntity>(
                Builders<MaterialConsumptionEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_material_consumption_asset"
                }),

            new CreateIndexModel<MaterialConsumptionEntity>(
                Builders<MaterialConsumptionEntity>.IndexKeys
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_material_consumption_job"
                }),

            new CreateIndexModel<MaterialConsumptionEntity>(
                Builders<MaterialConsumptionEntity>.IndexKeys
                    .Ascending(x => x.TaskId),
                new CreateIndexOptions
                {
                    Name = "ix_material_consumption_task"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
