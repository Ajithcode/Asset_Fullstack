using A360.Repository.Repositories;
using ChecklistEntity = A360.Inspection.Domain.Entities.Checklist;

namespace A360.Inspection.Repository.Repositories;

public interface IChecklistRepository : IMongoRepository<ChecklistEntity>
{
    Task<ChecklistEntity?> GetByChecklistCodeAsync(string checklistCode, CancellationToken cancellationToken = default);
}
