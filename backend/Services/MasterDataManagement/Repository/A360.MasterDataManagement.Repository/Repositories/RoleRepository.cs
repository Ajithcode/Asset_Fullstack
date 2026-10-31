using System.Text.RegularExpressions;
using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class RoleRepository : MongoRepository<RoleEntity>,
    IRoleRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_roles";

    public RoleRepository(IMongoDatabase database)
        : base(database.GetCollection<RoleEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<RoleEntity>> SearchAsync(
        RoleFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<RoleEntity>.Filter;
        var filters = new List<FilterDefinition<RoleEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.RoleName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filters.Add(builder.Regex(x => x.Type, ExactIgnoreCase(filter.Type)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.RoleName)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<RoleEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithPermissionAsync(
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<RoleEntity>.Filter.ElemMatch(x => x.Permissions, r => r.Id == permissionId))
            .AnyAsync(cancellationToken);
    }

    public async Task UpdatePermissionNameAsync(
        string permissionId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        // Each record lists an entry at most once, so the positional "$" update reaches it.
        await Collection.UpdateManyAsync(
            Builders<RoleEntity>.Filter.ElemMatch(x => x.Permissions, r => r.Id == permissionId),
            Builders<RoleEntity>.Update.Set("permissions.$.name", permissionName),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> AnyWithModuleAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<RoleEntity>.Filter.ElemMatch(x => x.ModuleAccess, r => r.Id == moduleId))
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateModuleNameAsync(
        string moduleId,
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        // Each record lists an entry at most once, so the positional "$" update reaches it.
        await Collection.UpdateManyAsync(
            Builders<RoleEntity>.Filter.ElemMatch(x => x.ModuleAccess, r => r.Id == moduleId),
            Builders<RoleEntity>.Update.Set("module_access.$.name", moduleName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<RoleEntity>(
                Builders<RoleEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_role_client_code"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
