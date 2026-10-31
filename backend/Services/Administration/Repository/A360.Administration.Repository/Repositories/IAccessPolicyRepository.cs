using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IAccessPolicyRepository : IMongoRepository<AccessPolicy>
{
    Task<AccessPolicy?> GetByPolicyIdAsync(string policyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AccessPolicy>> GetAsync(
        string? tenantId,
        string? clientId,
        string? policyType,
        string? status,
        CancellationToken cancellationToken = default);

    // excludePolicyId is the policy being updated, so it does not clash with itself.
    Task<bool> PolicyCodeExistsAsync(
        string policyCode,
        string? tenantId,
        string? excludePolicyId = null,
        CancellationToken cancellationToken = default);

    // True when any non-deleted policy lists the role in its assigned roles.
    Task<bool> AnyAssignedToRoleAsync(string roleId, CancellationToken cancellationToken = default);
}
