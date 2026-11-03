using A360.Repository.Repositories;
using InspectionTypeEntity = A360.Inspection.Domain.Entities.InspectionType;

namespace A360.Inspection.Repository.Repositories;

public interface IInspectionTypeRepository : IMongoRepository<InspectionTypeEntity>
{
    Task<InspectionTypeEntity?> GetByInspectionTypeCodeAsync(string inspectionTypeCode, CancellationToken cancellationToken = default);
}
