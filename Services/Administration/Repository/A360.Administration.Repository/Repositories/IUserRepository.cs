using A360.Repository.Repositories;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Repository.Repositories;

public interface IUserRepository : IMongoRepository<User>
{
    Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, string? tenantId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, string? tenantId, CancellationToken cancellationToken = default);
}
