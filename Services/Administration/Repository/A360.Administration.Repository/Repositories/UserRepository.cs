using MongoDB.Driver;
using A360.Repository.Repositories;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Repository.Repositories;

public sealed class UserRepository : MongoRepository<User>, IUserRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "user_management_users";

    public UserRepository(IMongoDatabase database)
        : base(database.GetCollection<User>(CollectionName))
    {
    }

    public async Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await Collection
            .Find(user => user.UserId == userId && !user.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetUsersAsync(string? tenantId, string? clientId, CancellationToken cancellationToken = default)
    {
        var builder = Builders<User>.Filter;
        var filter = builder.Eq(user => user.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(user => user.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(user => user.ClientId, clientId);
        }

        return await Collection
            .Find(filter)
            .SortByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetBySignInNameAsync(string signInName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signInName))
        {
            return null;
        }

        // Usernames and emails are stored lower-case
        var name = signInName.Trim().ToLowerInvariant();
        return await Collection
            .Find(user => !user.IsDeleted && (user.Email == name || user.Username == name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> CountWithRoleAsync(string roleName, string? tenantId, CancellationToken cancellationToken = default)
    {
        var builder = Builders<User>.Filter;
        var filter = builder.Eq(user => user.IsDeleted, false)
            & (builder.Eq(user => user.PrimaryRole, roleName) | builder.AnyEq(user => user.SecondaryRoles, roleName));
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(user => user.TenantId, tenantId);
        }

        return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task RenameRoleAsync(string oldName, string newName, string? tenantId, CancellationToken cancellationToken = default)
    {
        var builder = Builders<User>.Filter;
        var scope = builder.Eq(user => user.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            scope &= builder.Eq(user => user.TenantId, tenantId);
        }

        await Collection.UpdateManyAsync(
            scope & builder.Eq(user => user.PrimaryRole, oldName),
            Builders<User>.Update.Set(user => user.PrimaryRole, newName),
            cancellationToken: cancellationToken);
        await Collection.UpdateManyAsync(
            scope & builder.AnyEq(user => user.SecondaryRoles, oldName),
            Builders<User>.Update.Set("secondary_roles.$", newName),
            cancellationToken: cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, string? tenantId, CancellationToken cancellationToken = default, string? exceptUserId = null)
    {
        return ExistsAsync(Builders<User>.Filter.Eq(user => user.Username, username), tenantId, exceptUserId, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, string? tenantId, CancellationToken cancellationToken = default, string? exceptUserId = null)
    {
        return ExistsAsync(Builders<User>.Filter.Eq(user => user.Email, email), tenantId, exceptUserId, cancellationToken);
    }

    private async Task<bool> ExistsAsync(FilterDefinition<User> filter, string? tenantId, string? exceptUserId, CancellationToken cancellationToken)
    {
        var builder = Builders<User>.Filter;
        filter &= builder.Eq(user => user.IsDeleted, false) & builder.Eq(user => user.TenantId, tenantId);
        if (!string.IsNullOrWhiteSpace(exceptUserId))
        {
            filter &= builder.Ne(user => user.UserId, exceptUserId);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(user => user.UserId),
                new CreateIndexOptions { Name = "ux_user_management_users_user_id", Unique = true }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(user => user.TenantId)
                    .Ascending(user => user.Username),
                new CreateIndexOptions { Name = "ix_user_management_users_tenant_id_username" }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(user => user.TenantId)
                    .Ascending(user => user.Email),
                new CreateIndexOptions { Name = "ix_user_management_users_tenant_id_email" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
