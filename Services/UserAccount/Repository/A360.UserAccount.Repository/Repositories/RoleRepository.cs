using MongoDB.Driver;
using A360.Repository.Repositories;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;

namespace A360.UserAccount.Repository.Repositories;

public sealed class RoleRepository : MongoRepository<RoleEntity>, IRoleRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "Roles";

    public RoleRepository(IMongoDatabase database)
        : base(database.GetCollection<RoleEntity>(CollectionName))
    {
    }

    public async Task<RoleEntity?> GetByRoleIdAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        return await Collection
            .Find(role => role.RoleId == roleId.Trim())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RoleExistsAsync(
        string roleIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleIdentifier))
        {
            return false;
        }

        var trimmedRoleIdentifier = roleIdentifier.Trim();
        if (MongoObjectId.IsValid(trimmedRoleIdentifier))
        {
            return await GetByIdAsync(trimmedRoleIdentifier, cancellationToken) is not null;
        }

        return await GetByRoleIdAsync(trimmedRoleIdentifier, cancellationToken) is not null;
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<RoleEntity>(
                Builders<RoleEntity>.IndexKeys.Ascending(role => role.RoleId),
                new CreateIndexOptions { Name = "ux_roles_role_id", Unique = true }),
            new CreateIndexModel<RoleEntity>(
                Builders<RoleEntity>.IndexKeys
                    .Ascending(role => role.ClientId)
                    .Ascending(role => role.RoleName),
                new CreateIndexOptions { Name = "ix_roles_client_role_name" }),
            new CreateIndexModel<RoleEntity>(
                Builders<RoleEntity>.IndexKeys.Ascending("AssignedProject.project_id"),
                new CreateIndexOptions { Name = "ix_roles_assigned_project_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
