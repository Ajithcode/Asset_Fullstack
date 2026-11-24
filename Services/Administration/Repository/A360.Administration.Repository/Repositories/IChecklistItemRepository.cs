using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IChecklistItemRepository : IMongoRepository<ChecklistItem>
{
    Task<ChecklistItem?> GetByItemIdAsync(string itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChecklistItem>> GetAsync(
        string? tenantId,
        string? clientId,
        string? section,
        CancellationToken cancellationToken = default);
}
