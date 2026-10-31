using A360.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;

namespace A360.UserAccount.Repository.Repositories;

public interface IRefreshTokenRepository : IMongoRepository<RefreshTokenEntity>
{
    Task<RefreshTokenEntity?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string tokenHash, string? replacedByTokenHash = null, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(string userRefId, CancellationToken cancellationToken = default);
}
