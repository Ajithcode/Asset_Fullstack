using MongoDB.Bson;
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

    public async Task<Integration?> GetByIntegrationIdAsync(string integrationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(integrationId))
        {
            return null;
        }

        return await Collection
            .Find(integration => integration.IntegrationId == integrationId && !integration.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Integration>> GetAsync(
        string? tenantId,
        string? clientId,
        string? environment,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Integration>.Filter;
        var filter = builder.Eq(integration => integration.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(integration => integration.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(integration => integration.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= builder.Regex(integration => integration.Environment, ExactIgnoreCase(environment));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(integration => integration.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(integration => integration.IntegrationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IntegrationCodeExistsAsync(
        string integrationCode,
        string? tenantId,
        CancellationToken cancellationToken = default,
        string? excludeIntegrationId = null)
    {
        var builder = Builders<Integration>.Filter;
        var filter = builder.Eq(integration => integration.IntegrationCode, integrationCode)
            & builder.Eq(integration => integration.IsDeleted, false)
            & builder.Eq(integration => integration.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeIntegrationId))
        {
            filter &= builder.Ne(integration => integration.IntegrationId, excludeIntegrationId);
        }

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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
