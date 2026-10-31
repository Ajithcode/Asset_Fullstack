using A360.Repository.Repositories;
using DefectEntity = A360.Inspection.Domain.Entities.Defect;

namespace A360.Inspection.Repository.Repositories;

public interface IDefectRepository : IMongoRepository<DefectEntity>
{
    Task<DefectEntity?> GetByDefectCodeAsync(string defectCode, CancellationToken cancellationToken = default);
}
