using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface ILocationRepository : IMongoRepository<Location>
{
    Task<Location?> GetByLocationIdAsync(string locationId, CancellationToken cancellationToken = default);

    Task<bool> LocationCodeExistsAsync(string locationCode, string? tenantId, CancellationToken cancellationToken = default);
}
