using MongoDB.Driver;
using A360.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;

namespace A360.UserAccount.Repository.Repositories;

public sealed class RefreshTokenRepository : MongoRepository<RefreshTokenEntity>, IRefreshTokenRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "RefreshTokens";

    public RefreshTokenRepository(IMongoDatabase database)
        : base(database.GetCollection<RefreshTokenEntity>(CollectionName))
    {
    }

    public async Task<RefreshTokenEntity?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(token => token.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        string tokenHash,
        string? replacedByTokenHash = null,
        CancellationToken cancellationToken = default)
    {
        // Only an active token can be revoked, so two concurrent refreshes cannot both succeed.
        var result = await Collection.UpdateOneAsync(
            token => token.TokenHash == tokenHash && token.RevokedAt == null,
            Builders<RefreshTokenEntity>.Update
                .Set(token => token.RevokedAt, DateTime.UtcNow)
                .Set(token => token.ReplacedByTokenHash, replacedByTokenHash)
                .Set(token => token.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task RevokeAllForUserAsync(string userRefId, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            token => token.UserRefId == userRefId && token.RevokedAt == null,
            Builders<RefreshTokenEntity>.Update
                .Set(token => token.RevokedAt, DateTime.UtcNow)
                .Set(token => token.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<RefreshTokenEntity>(
                Builders<RefreshTokenEntity>.IndexKeys.Ascending(token => token.TokenHash),
                new CreateIndexOptions { Name = "ux_refresh_tokens_token_hash", Unique = true }),
            new CreateIndexModel<RefreshTokenEntity>(
                Builders<RefreshTokenEntity>.IndexKeys.Ascending(token => token.UserRefId),
                new CreateIndexOptions { Name = "ix_refresh_tokens_user_ref_id" }),
            new CreateIndexModel<RefreshTokenEntity>(
                Builders<RefreshTokenEntity>.IndexKeys.Ascending(token => token.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_refresh_tokens_expires_at", ExpireAfter = TimeSpan.Zero })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
