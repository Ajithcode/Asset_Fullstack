
using PerformanceMetricEntity = A360.Maintenance.Domain.Entities.PerformanceMetric;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IPerformanceMetricRepository : IMongoRepository<PerformanceMetricEntity>
{
    Task<IReadOnlyCollection<PerformanceMetricEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
