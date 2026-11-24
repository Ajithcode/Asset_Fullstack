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

    public async Task<long> RevokeOthersForUserAsync(string userRefId, string? exceptTokenHash, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RefreshTokenEntity>.Filter.Where(token => token.UserRefId == userRefId && token.RevokedAt == null);
        if (!string.IsNullOrWhiteSpace(exceptTokenHash))
        {
            filter &= Builders<RefreshTokenEntity>.Filter.Ne(token => token.TokenHash, exceptTokenHash);
        }

        var result = await Collection.UpdateManyAsync(
            filter,
            Builders<RefreshTokenEntity>.Update
                .Set(token => token.RevokedAt, DateTime.UtcNow)
                .Set(token => token.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged ? result.ModifiedCount : 0;
    }

    public async Task<IReadOnlyList<RefreshTokenEntity>> GetEndedUnloggedSessionsAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(token => token.RevokedAt == null && token.ExpiresAt <= now && token.SessionEndLoggedAt == null)
            .SortBy(token => token.ExpiresAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkSessionEndLoggedAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await Collection.UpdateOneAsync(
            token => token.Id == id && token.SessionEndLoggedAt == null,
            Builders<RefreshTokenEntity>.Update.Set(token => token.SessionEndLoggedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    /// <summary>
    /// Expired tokens are kept this long before MongoDB deletes them, so a sign-out with an expired token can still be
    /// tied to its user and the "session expired" audit event can be written. Expired tokens are refused regardless.
    /// </summary>
    public static readonly TimeSpan ExpiredTokenRetention = TimeSpan.FromDays(2);

    private const string TtlIndexName = "ttl_refresh_tokens_expires_at";

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        // The TTL index used to delete tokens the moment they expired: recreate it with the retention period
        using (var cursor = await Collection.Indexes.ListAsync(cancellationToken))
        {
            var existing = (await cursor.ToListAsync(cancellationToken))
                .FirstOrDefault(index => index.GetValue("name", string.Empty).AsString == TtlIndexName);
            if (existing is not null
                && (!existing.TryGetValue("expireAfterSeconds", out var seconds)
                    || !seconds.IsNumeric
                    || (long)seconds.ToDouble() != (long)ExpiredTokenRetention.TotalSeconds))
            {
                await Collection.Indexes.DropOneAsync(TtlIndexName, cancellationToken);
            }
        }

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
                new CreateIndexOptions { Name = TtlIndexName, ExpireAfter = ExpiredTokenRetention }),
            new CreateIndexModel<RefreshTokenEntity>(
                Builders<RefreshTokenEntity>.IndexKeys.Ascending(token => token.SessionEndLoggedAt).Ascending(token => token.RevokedAt).Ascending(token => token.ExpiresAt),
                new CreateIndexOptions { Name = "ix_refresh_tokens_session_end" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
