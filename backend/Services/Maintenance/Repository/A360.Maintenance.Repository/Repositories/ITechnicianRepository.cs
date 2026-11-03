
using TechnicianEntity = A360.Maintenance.Domain.Entities.Technician;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface ITechnicianRepository : IMongoRepository<TechnicianEntity>
{
    Task<IReadOnlyCollection<TechnicianEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
