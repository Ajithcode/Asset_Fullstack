using A360.Repository.Repositories;
using TaskCategoryEntity = A360.Inspection.Domain.Entities.TaskCategory;

namespace A360.Inspection.Repository.Repositories;

public interface ITaskCategoryRepository : IMongoRepository<TaskCategoryEntity>
{
    Task<TaskCategoryEntity?> GetByCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default);
}
