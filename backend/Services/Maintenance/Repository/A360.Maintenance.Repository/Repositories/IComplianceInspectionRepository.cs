
using ComplianceInspectionEntity = A360.Maintenance.Domain.Entities.ComplianceInspection;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IComplianceInspectionRepository : IMongoRepository<ComplianceInspectionEntity>
{
    Task<IReadOnlyCollection<ComplianceInspectionEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
