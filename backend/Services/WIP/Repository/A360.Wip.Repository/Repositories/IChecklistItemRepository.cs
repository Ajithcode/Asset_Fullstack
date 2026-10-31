
using ChecklistItemEntity = A360.Wip.Domain.Entities.ChecklistItem;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IChecklistItemRepository : IMongoRepository<ChecklistItemEntity>
{
    Task<IReadOnlyCollection<ChecklistItemEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChecklistItemEntity>> GetByChecklistIdAsync(string checklistId, CancellationToken cancellationToken = default);
}
