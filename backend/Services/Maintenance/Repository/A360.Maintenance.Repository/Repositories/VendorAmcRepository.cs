
using VendorAmcEntity = A360.Maintenance.Domain.Entities.VendorAmc;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class VendorAmcRepository : MongoRepository<VendorAmcEntity>,
    IVendorAmcRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "vendor_amc_contracts";

    public VendorAmcRepository(IMongoDatabase database)
        : base(database.GetCollection<VendorAmcEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<VendorAmcEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<VendorAmcEntity>(
                Builders<VendorAmcEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_vendor_amc_client"
                }),
            new CreateIndexModel<VendorAmcEntity>(
                Builders<VendorAmcEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_vendor_amc_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
