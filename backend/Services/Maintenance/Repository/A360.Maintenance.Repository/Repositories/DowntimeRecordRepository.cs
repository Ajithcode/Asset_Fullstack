
using DowntimeRecordEntity = A360.Maintenance.Domain.Entities.DowntimeRecord;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class DowntimeRecordRepository : MongoRepository<DowntimeRecordEntity>,
    IDowntimeRecordRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "downtime_records";

    public DowntimeRecordRepository(IMongoDatabase database)
        : base(database.GetCollection<DowntimeRecordEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<DowntimeRecordEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<DowntimeRecordEntity>(
                Builders<DowntimeRecordEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_downtime_record_client"
                }),
            new CreateIndexModel<DowntimeRecordEntity>(
                Builders<DowntimeRecordEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_downtime_record_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
