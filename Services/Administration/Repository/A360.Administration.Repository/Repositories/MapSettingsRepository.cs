using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapSettingsRepository : MongoRepository<MapSettings>, IMapSettingsRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_settings";

    public MapSettingsRepository(IMongoDatabase database)
        : base(database.GetCollection<MapSettings>(CollectionName))
    {
    }

    public async Task<MapSettings?> GetByScopeAsync(string? clientId, string? tenantId, CancellationToken cancellationToken = default)
    {
        var builder = Builders<MapSettings>.Filter;
        var filter = builder.Eq(settings => settings.ClientId, clientId)
            & builder.Eq(settings => settings.TenantId, tenantId)
            & builder.Eq(settings => settings.IsDeleted, false);

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapSettings>(
                Builders<MapSettings>.IndexKeys.Ascending(settings => settings.SettingsId),
                new CreateIndexOptions { Name = "ux_maps_settings_settings_id", Unique = true }),
            // Guarantees a single settings document per tenant/client even under concurrent saves.
            new CreateIndexModel<MapSettings>(
                Builders<MapSettings>.IndexKeys
                    .Ascending(settings => settings.TenantId)
                    .Ascending(settings => settings.ClientId),
                new CreateIndexOptions { Name = "ux_maps_settings_tenant_id_client_id", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
