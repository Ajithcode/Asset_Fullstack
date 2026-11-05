using A360.Repository.Repositories;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Repository.Repositories;

public interface IUserRepository : IMongoRepository<User>
{
    Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Users that are not deleted, newest first; tenant / client filters apply when given.</summary>
    Task<IReadOnlyCollection<User>> GetUsersAsync(string? tenantId, string? clientId, CancellationToken cancellationToken = default);

    /// <summary>The user (not deleted) whose email or username is the given sign-in name, case-insensitive.</summary>
    Task<User?> GetBySignInNameAsync(string signInName, CancellationToken cancellationToken = default);

    /// <summary>Number of users (not deleted) with the role as primary or secondary role.</summary>
    Task<long> CountWithRoleAsync(string roleName, string? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Points users with the old role name at the new one (users keep roles by name).</summary>
    Task RenameRoleAsync(string oldName, string newName, string? tenantId, CancellationToken cancellationToken = default);

    /// <param name="exceptUserId">User being edited, left out of the check.</param>
    Task<bool> UsernameExistsAsync(string username, string? tenantId, CancellationToken cancellationToken = default, string? exceptUserId = null);

    /// <param name="exceptUserId">User being edited, left out of the check.</param>
    Task<bool> EmailExistsAsync(string email, string? tenantId, CancellationToken cancellationToken = default, string? exceptUserId = null);
}
