using A360.Repository.Repositories;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Repository.Repositories;

public interface IUserRepository : IMongoRepository<UserEntity>
{
    Task<bool> EmailExistsAsync(string email, string? excludedId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserEntity>> GetByRoleIdAsync(string roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserEntity>> GetByUserNameAsync(string userName, int limit, CancellationToken cancellationToken = default);

    Task SetTwoFactorCodeAsync(string id, string codeHash, DateTime expiration, CancellationToken cancellationToken = default);

    Task<int> RegisterFailedTwoFactorAttemptAsync(string id, CancellationToken cancellationToken = default);

    Task ClearTwoFactorCodeAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> CompleteTwoFactorLoginAsync(string id, string codeHash, CancellationToken cancellationToken = default);

    /// <summary>Stores the (encrypted) secret shown as a QR code until the user confirms it.</summary>
    Task SetPendingTotpSecretAsync(string id, string protectedSecret, CancellationToken cancellationToken = default);

    /// <summary>Confirms setup: the pending secret becomes the active one and authenticator sign-in is on.</summary>
    Task<bool> EnableTotpAsync(string id, string protectedSecret, long usedStep, CancellationToken cancellationToken = default);

    /// <summary>Records an accepted code's time step; false when that step (or a later one) was already used.</summary>
    Task<bool> TryUseTotpStepAsync(string id, long step, CancellationToken cancellationToken = default);
}
