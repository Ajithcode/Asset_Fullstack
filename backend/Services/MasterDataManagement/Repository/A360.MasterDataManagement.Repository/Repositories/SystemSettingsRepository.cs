using SystemSettingsEntity = A360.MasterDataManagement.Domain.Entities.SystemSettings;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class SystemSettingsRepository : MongoRepository<SystemSettingsEntity>,
    ISystemSettingsRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_system_settings";

    public SystemSettingsRepository(IMongoDatabase database)
        : base(database.GetCollection<SystemSettingsEntity>(CollectionName))
    {
    }

    public async Task<SystemSettingsEntity?> GetByClientAsync(
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ClientId == clientId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SystemSettingsEntity>(
                Builders<SystemSettingsEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_master_system_settings_client",
                    Unique = true
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
