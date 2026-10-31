using MongoDB.Driver;
using A360.Repository.Repositories;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Repository.Repositories;

public sealed class UserRepository : MongoRepository<UserEntity>, IUserRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "Users";
    private static readonly Collation CaseInsensitiveCollation = new("en", strength: CollationStrength.Secondary);

    public UserRepository(IMongoDatabase database)
        : base(database.GetCollection<UserEntity>(CollectionName))
    {
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        string? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var emailFilter = Builders<UserEntity>.Filter.Eq(user => user.Email, email.Trim());

        var filter = MongoObjectId.IsValid(excludedId)
            ? Builders<UserEntity>.Filter.And(
                emailFilter,
                Builders<UserEntity>.Filter.Ne(user => user.Id, excludedId))
            : emailFilter;

        return await Collection
            .Find(filter, new FindOptions { Collation = CaseInsensitiveCollation })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserEntity>> GetByRoleIdAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(user => user.UserRoleId == roleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserEntity>> GetByUserNameAsync(
        string userName,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(user => user.UserName == userName && !user.IsDeleted, new FindOptions { Collation = CaseInsensitiveCollation })
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SetTwoFactorCodeAsync(
        string id,
        string codeHash,
        DateTime expiration,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateOneAsync(
            user => user.Id == id,
            Builders<UserEntity>.Update
                .Set(user => user.TwoFactorCode, codeHash)
                .Set(user => user.TwoFactorExpiration, expiration)
                .Set(user => user.TwoFactorAttempts, 0),
            cancellationToken: cancellationToken);
    }

    public async Task<int> RegisterFailedTwoFactorAttemptAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await Collection.FindOneAndUpdateAsync(
            user => user.Id == id,
            Builders<UserEntity>.Update.Inc(user => user.TwoFactorAttempts, 1),
            new FindOneAndUpdateOptions<UserEntity> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return user?.TwoFactorAttempts ?? int.MaxValue;
    }

    public async Task ClearTwoFactorCodeAsync(string id, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateOneAsync(
            user => user.Id == id,
            ClearTwoFactorUpdate(),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> CompleteTwoFactorLoginAsync(
        string id,
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        // Matching on the code hash makes the OTP single-use even under concurrent requests.
        var result = await Collection.UpdateOneAsync(
            user => user.Id == id && user.TwoFactorCode == codeHash,
            ClearTwoFactorUpdate().Set(user => user.LastLogin, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task SetPendingTotpSecretAsync(
        string id,
        string protectedSecret,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateOneAsync(
            user => user.Id == id,
            Builders<UserEntity>.Update.Set(user => user.TotpPendingSecret, protectedSecret),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> EnableTotpAsync(
        string id,
        string protectedSecret,
        long usedStep,
        CancellationToken cancellationToken = default)
    {
        // Matching on the pending secret: two setups racing cannot both enable different secrets.
        var result = await Collection.UpdateOneAsync(
            user => user.Id == id && user.TotpPendingSecret == protectedSecret,
            Builders<UserEntity>.Update
                .Set(user => user.TotpSecret, protectedSecret)
                .Set(user => user.TotpPendingSecret, string.Empty)
                .Set(user => user.TotpEnabled, true)
                .Set(user => user.TotpLastUsedStep, usedStep),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> TryUseTotpStepAsync(string id, long step, CancellationToken cancellationToken = default)
    {
        // Conditional update: the same code cannot be accepted twice, even by concurrent requests.
        var result = await Collection.UpdateOneAsync(
            user => user.Id == id && user.TotpLastUsedStep < step,
            Builders<UserEntity>.Update.Set(user => user.TotpLastUsedStep, step),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    private static UpdateDefinition<UserEntity> ClearTwoFactorUpdate()
    {
        return Builders<UserEntity>.Update
            .Set(user => user.TwoFactorCode, string.Empty)
            .Set(user => user.TwoFactorExpiration, null)
            .Set(user => user.TwoFactorAttempts, 0);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<UserEntity>(
                Builders<UserEntity>.IndexKeys.Ascending(user => user.UserName),
                new CreateIndexOptions { Name = "ix_users_user_name", Collation = CaseInsensitiveCollation }),
            new CreateIndexModel<UserEntity>(
                Builders<UserEntity>.IndexKeys.Ascending(user => user.UserId),
                new CreateIndexOptions { Name = "ux_users_user_id", Unique = true }),
            new CreateIndexModel<UserEntity>(
                Builders<UserEntity>.IndexKeys.Ascending(user => user.Email),
                new CreateIndexOptions
                {
                    Name = "ux_users_email",
                    Unique = true,
                    Collation = CaseInsensitiveCollation
                }),
            new CreateIndexModel<UserEntity>(
                Builders<UserEntity>.IndexKeys.Ascending(user => user.UserRoleId),
                new CreateIndexOptions { Name = "ix_users_user_role_id" }),
            new CreateIndexModel<UserEntity>(
                Builders<UserEntity>.IndexKeys
                    .Ascending(user => user.ClientId)
                    .Ascending(user => user.LoginStatus),
                new CreateIndexOptions { Name = "ix_users_client_login_status" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
