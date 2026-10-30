using A360.Repository.Repositories;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;

namespace A360.UserAccount.Repository.Repositories;

public interface IRoleRepository : IMongoRepository<RoleEntity>
{
    Task<RoleEntity?> GetByRoleIdAsync(string roleId, CancellationToken cancellationToken = default);

    Task<bool> RoleExistsAsync(string roleIdentifier, CancellationToken cancellationToken = default);
}
