using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class SystemConfigurationRepository
    : MongoRepository<SystemConfiguration>, ISystemConfigurationRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_system_configurations";

    public SystemConfigurationRepository(IMongoDatabase database)
        : base(database.GetCollection<SystemConfiguration>(CollectionName))
    {
    }

    public async Task<SystemConfiguration?> GetByScopeAsync(
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SystemConfiguration>.Filter;
        var filter = builder.Eq(configuration => configuration.TenantId, tenantId)
            & builder.Eq(configuration => configuration.ClientId, clientId);

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SystemConfiguration>(
                Builders<SystemConfiguration>.IndexKeys.Ascending(configuration => configuration.ConfigurationId),
                new CreateIndexOptions { Name = "ux_administration_system_configurations_configuration_id", Unique = true }),
            // Guarantees a single configuration per tenant/client even under concurrent saves.
            new CreateIndexModel<SystemConfiguration>(
                Builders<SystemConfiguration>.IndexKeys
                    .Ascending(configuration => configuration.TenantId)
                    .Ascending(configuration => configuration.ClientId),
                new CreateIndexOptions { Name = "ux_administration_system_configurations_tenant_id_client_id", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
