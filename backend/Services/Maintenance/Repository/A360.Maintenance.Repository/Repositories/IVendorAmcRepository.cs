
using VendorAmcEntity = A360.Maintenance.Domain.Entities.VendorAmc;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IVendorAmcRepository : IMongoRepository<VendorAmcEntity>
{
    Task<IReadOnlyCollection<VendorAmcEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
