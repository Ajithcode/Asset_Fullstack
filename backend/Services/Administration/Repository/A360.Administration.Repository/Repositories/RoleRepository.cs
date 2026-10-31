using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class RoleRepository : MongoRepository<Role>, IRoleRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_roles";

    public RoleRepository(IMongoDatabase database)
        : base(database.GetCollection<Role>(CollectionName))
    {
    }

    public async Task<Role?> GetByRoleIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        return await Collection
            .Find(role => role.RoleId == roleId && !role.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> GetByRoleIdsAsync(
        IReadOnlyCollection<string> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var builder = Builders<Role>.Filter;
        var filter = builder.In(role => role.RoleId, roleIds) & builder.Eq(role => role.IsDeleted, false);

        return await Collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> GetAsync(
        string? tenantId,
        string? clientId,
        string? roleType,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Role>.Filter;
        var filter = builder.Eq(role => role.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(role => role.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(role => role.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(roleType))
        {
            filter &= builder.Regex(role => role.RoleType, ExactIgnoreCase(roleType));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(role => role.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(role => role.RoleName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RoleCodeExistsAsync(
        string roleCode,
        string? tenantId,
        string? excludeRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Role>.Filter;
        var filter = builder.Eq(role => role.RoleCode, roleCode)
            & builder.Eq(role => role.IsDeleted, false)
            & builder.Eq(role => role.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeRoleId))
        {
            filter &= builder.Ne(role => role.RoleId, excludeRoleId);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys.Ascending(role => role.RoleId),
                new CreateIndexOptions { Name = "ux_administration_roles_role_id", Unique = true }),
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys
                    .Ascending(role => role.TenantId)
                    .Ascending(role => role.RoleCode),
                new CreateIndexOptions { Name = "ix_administration_roles_tenant_id_role_code" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
