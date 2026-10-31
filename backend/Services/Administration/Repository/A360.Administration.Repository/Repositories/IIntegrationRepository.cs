using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IIntegrationRepository : IMongoRepository<Integration>
{
    Task<Integration?> GetByIntegrationIdAsync(string integrationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Integration>> GetAsync(
        string? tenantId,
        string? clientId,
        string? environment,
        string? status,
        CancellationToken cancellationToken = default);

    // excludeIntegrationId is the integration being updated, so it does not clash with itself.
    Task<bool> IntegrationCodeExistsAsync(
        string integrationCode,
        string? tenantId,
        CancellationToken cancellationToken = default,
        string? excludeIntegrationId = null);
}
