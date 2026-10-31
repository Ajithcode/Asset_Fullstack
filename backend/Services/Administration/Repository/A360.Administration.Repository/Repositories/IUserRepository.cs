using A360.Repository.Repositories;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Repository.Repositories;

public interface IUserRepository : IMongoRepository<User>
{
    Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<User>> GetAsync(
        string? tenantId,
        string? clientId,
        string? accountStatus,
        string? primaryRole,
        CancellationToken cancellationToken = default);

    // excludeUserId is the user being updated, so it does not clash with itself.
    Task<bool> UsernameExistsAsync(
        string username,
        string? tenantId,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string email,
        string? tenantId,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default);
}
