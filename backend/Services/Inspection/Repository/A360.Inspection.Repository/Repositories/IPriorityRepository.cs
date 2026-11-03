using A360.Repository.Repositories;
using PriorityEntity = A360.Inspection.Domain.Entities.Priority;

namespace A360.Inspection.Repository.Repositories;

public interface IPriorityRepository : IMongoRepository<PriorityEntity>
{
    Task<PriorityEntity?> GetByPriorityCodeAsync(string priorityCode, CancellationToken cancellationToken = default);
}
