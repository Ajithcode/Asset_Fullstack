using A360.Repository.Repositories;
using InspectionTaskEntity = A360.Inspection.Domain.Entities.InspectionTask;

namespace A360.Inspection.Repository.Repositories;

public interface IInspectionTaskRepository : IMongoRepository<InspectionTaskEntity>
{
    Task<InspectionTaskEntity?> GetByTaskCodeAsync(string taskCode, CancellationToken cancellationToken = default);
}
