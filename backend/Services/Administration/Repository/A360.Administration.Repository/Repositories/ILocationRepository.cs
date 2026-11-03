using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface ILocationRepository : IMongoRepository<Location>
{
    Task<Location?> GetByLocationIdAsync(string locationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Location>> GetByLocationIdsAsync(IReadOnlyCollection<string> locationIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Location>> GetAsync(
        string? tenantId,
        string? clientId,
        string? locationType,
        string? status,
        CancellationToken cancellationToken = default);

    // excludeLocationId is the location being updated, so it does not clash with itself.
    Task<bool> LocationCodeExistsAsync(
        string locationCode,
        string? tenantId,
        string? excludeLocationId = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string locationId, CancellationToken cancellationToken = default);
}
