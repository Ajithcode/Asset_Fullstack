using MongoDB.Bson;
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

    public async Task<IReadOnlyCollection<User>> GetAsync(
        string? tenantId,
        string? clientId,
        string? accountStatus,
        string? primaryRole,
        CancellationToken cancellationToken = default)
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

        if (!string.IsNullOrWhiteSpace(accountStatus))
        {
            filter &= builder.Regex(user => user.Status, ExactIgnoreCase(accountStatus));
        }

        if (!string.IsNullOrWhiteSpace(primaryRole))
        {
            filter &= builder.Regex(user => user.PrimaryRole, ExactIgnoreCase(primaryRole));
        }

        return await Collection
            .Find(filter)
            .SortByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(
        string username,
        string? tenantId,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        return ExistsAsync(Builders<User>.Filter.Eq(user => user.Username, username), tenantId, excludeUserId, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(
        string email,
        string? tenantId,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        return ExistsAsync(Builders<User>.Filter.Eq(user => user.Email, email), tenantId, excludeUserId, cancellationToken);
    }

    private async Task<bool> ExistsAsync(
        FilterDefinition<User> filter,
        string? tenantId,
        string? excludeUserId,
        CancellationToken cancellationToken)
    {
        var builder = Builders<User>.Filter;
        filter &= builder.Eq(user => user.IsDeleted, false) & builder.Eq(user => user.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            filter &= builder.Ne(user => user.UserId, excludeUserId);
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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
