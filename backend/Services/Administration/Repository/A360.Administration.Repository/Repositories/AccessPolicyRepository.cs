using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class AccessPolicyRepository : MongoRepository<AccessPolicy>, IAccessPolicyRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_access_policies";

    public AccessPolicyRepository(IMongoDatabase database)
        : base(database.GetCollection<AccessPolicy>(CollectionName))
    {
    }

    public async Task<AccessPolicy?> GetByPolicyIdAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyId))
        {
            return null;
        }

        return await Collection
            .Find(policy => policy.PolicyId == policyId && !policy.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccessPolicy>> GetAsync(
        string? tenantId,
        string? clientId,
        string? policyType,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AccessPolicy>.Filter;
        var filter = builder.Eq(policy => policy.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(policy => policy.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(policy => policy.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(policyType))
        {
            filter &= builder.Regex(policy => policy.PolicyType, ExactIgnoreCase(policyType));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(policy => policy.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(policy => policy.PolicyName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PolicyCodeExistsAsync(
        string policyCode,
        string? tenantId,
        string? excludePolicyId = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AccessPolicy>.Filter;
        var filter = builder.Eq(policy => policy.PolicyCode, policyCode)
            & builder.Eq(policy => policy.IsDeleted, false)
            & builder.Eq(policy => policy.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludePolicyId))
        {
            filter &= builder.Ne(policy => policy.PolicyId, excludePolicyId);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyAssignedToRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AccessPolicy>.Filter;
        var filter = builder.AnyEq(policy => policy.AssignedRoleIds, roleId)
            & builder.Eq(policy => policy.IsDeleted, false);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AccessPolicy>(
                Builders<AccessPolicy>.IndexKeys.Ascending(policy => policy.PolicyId),
                new CreateIndexOptions { Name = "ux_administration_access_policies_policy_id", Unique = true }),
            new CreateIndexModel<AccessPolicy>(
                Builders<AccessPolicy>.IndexKeys
                    .Ascending(policy => policy.TenantId)
                    .Ascending(policy => policy.PolicyCode),
                new CreateIndexOptions { Name = "ix_administration_access_policies_tenant_id_policy_code" }),
            new CreateIndexModel<AccessPolicy>(
                Builders<AccessPolicy>.IndexKeys.Ascending(policy => policy.AssignedRoleIds),
                new CreateIndexOptions { Name = "ix_administration_access_policies_assigned_role_ids" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
