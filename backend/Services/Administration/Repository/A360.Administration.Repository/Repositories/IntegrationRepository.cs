using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class IntegrationRepository : MongoRepository<Integration>, IIntegrationRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_integrations";

    public IntegrationRepository(IMongoDatabase database)
        : base(database.GetCollection<Integration>(CollectionName))
    {
    }

    public async Task<bool> IntegrationCodeExistsAsync(
        string integrationCode,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Integration>.Filter;
        var filter = builder.Eq(integration => integration.IntegrationCode, integrationCode)
            & builder.Eq(integration => integration.IsDeleted, false)
            & builder.Eq(integration => integration.TenantId, tenantId);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Integration>(
                Builders<Integration>.IndexKeys.Ascending(integration => integration.IntegrationId),
                new CreateIndexOptions { Name = "ux_administration_integrations_integration_id", Unique = true }),
            new CreateIndexModel<Integration>(
                Builders<Integration>.IndexKeys
                    .Ascending(integration => integration.TenantId)
                    .Ascending(integration => integration.IntegrationCode),
                new CreateIndexOptions { Name = "ix_administration_integrations_tenant_id_integration_code" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
