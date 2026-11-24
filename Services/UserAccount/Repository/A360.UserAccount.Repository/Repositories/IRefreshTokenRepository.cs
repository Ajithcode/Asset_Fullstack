using A360.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;

namespace A360.UserAccount.Repository.Repositories;

public interface IRefreshTokenRepository : IMongoRepository<RefreshTokenEntity>
{
    Task<RefreshTokenEntity?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string tokenHash, string? replacedByTokenHash = null, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(string userRefId, CancellationToken cancellationToken = default);

    /// <summary>Revokes the user's active tokens except the one with <paramref name="exceptTokenHash"/>; returns how many.</summary>
    Task<long> RevokeOthersForUserAsync(string userRefId, string? exceptTokenHash, CancellationToken cancellationToken = default);

    /// <summary>Expired tokens that were never revoked (refreshed or signed out) and have no "session expired" event yet.</summary>
    Task<IReadOnlyList<RefreshTokenEntity>> GetEndedUnloggedSessionsAsync(DateTime now, int limit, CancellationToken cancellationToken = default);

    /// <summary>Marks the token's session end as logged; false when another instance already did.</summary>
    Task<bool> MarkSessionEndLoggedAsync(string id, CancellationToken cancellationToken = default);
}
