using MasterDataItemEntity = A360.MasterDataManagement.Domain.Entities.MasterDataItem;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class MasterDataItemRepository : MongoRepository<MasterDataItemEntity>,
    IMasterDataItemRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_data_items";

    public MasterDataItemRepository(IMongoDatabase database)
        : base(database.GetCollection<MasterDataItemEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MasterDataItemEntity>> GetByMasterTypeAsync(
        string masterType,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.MasterType == masterType)
            .SortBy(entity => entity.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MasterDataItemEntity>(
                Builders<MasterDataItemEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_master_data_item_client"
                }),
            new CreateIndexModel<MasterDataItemEntity>(
                Builders<MasterDataItemEntity>.IndexKeys
                    .Ascending(x => x.MasterType)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_data_item_type_code"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
