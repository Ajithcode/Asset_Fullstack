using A360.Repository.Repositories;
using InspectionFindingEntity = A360.Inspection.Domain.Entities.InspectionFinding;

namespace A360.Inspection.Repository.Repositories;

public interface IInspectionFindingRepository : IMongoRepository<InspectionFindingEntity>
{
    Task<InspectionFindingEntity?> GetByFindingCodeAsync(string findingCode, CancellationToken cancellationToken = default);
}
