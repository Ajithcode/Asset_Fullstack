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

    Task<bool> PolicyCodeExistsAsync(string policyCode, string? tenantId, CancellationToken cancellationToken = default);
}
