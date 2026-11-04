using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapGeneralSettingsRepository
    : MongoRepository<MapGeneralSettings>, IMapGeneralSettingsRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_map_general_settings";

    public MapGeneralSettingsRepository(IMongoDatabase database)
        : base(database.GetCollection<MapGeneralSettings>(CollectionName))
    {
    }

    public async Task<MapGeneralSettings?> GetBySettingsIdAsync(string settingsId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settingsId))
        {
            return null;
        }

        return await Collection
            .Find(settings => settings.SettingsId == settingsId && !settings.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MapGeneralSettings?> GetByScopeAsync(
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(settings => settings.TenantId == tenantId && settings.ClientId == clientId && !settings.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapGeneralSettings>(
                Builders<MapGeneralSettings>.IndexKeys.Ascending(settings => settings.SettingsId),
                new CreateIndexOptions { Name = "ux_administration_map_general_settings_settings_id", Unique = true }),
            new CreateIndexModel<MapGeneralSettings>(
                Builders<MapGeneralSettings>.IndexKeys
                    .Ascending(settings => settings.TenantId)
                    .Ascending(settings => settings.ClientId),
                new CreateIndexOptions { Name = "ix_administration_map_general_settings_tenant_id_client_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
