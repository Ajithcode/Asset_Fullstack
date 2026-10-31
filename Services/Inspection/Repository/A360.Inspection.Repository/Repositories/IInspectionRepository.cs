using A360.Repository.Repositories;
using InspectionEntity = A360.Inspection.Domain.Entities.Inspection;

namespace A360.Inspection.Repository.Repositories;

public interface IInspectionRepository : IMongoRepository<InspectionEntity>
{
    Task<InspectionEntity?> GetByInspectionCodeAsync(string inspectionCode, CancellationToken cancellationToken = default);
}
