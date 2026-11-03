using ChecklistEntity = A360.MasterDataManagement.Domain.Entities.Checklist;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IChecklistRepository : IMongoRepository<ChecklistEntity>
{
    Task<IReadOnlyCollection<ChecklistEntity>> SearchAsync(ChecklistFilter filter, CancellationToken cancellationToken = default);

    Task<ChecklistEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ChecklistFilter(
    string? Search,
    string? Status,
    string? ChecklistType,
    string? Category,
    string? ClientId);
