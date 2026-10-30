
using SparePartEntity = A360.Maintenance.Domain.Entities.SparePart;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface ISparePartRepository : IMongoRepository<SparePartEntity>
{
    Task<IReadOnlyCollection<SparePartEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
