using WorkTypeEntity = A360.MasterDataManagement.Domain.Entities.WorkType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IWorkTypeRepository : IMongoRepository<WorkTypeEntity>
{
    Task<IReadOnlyCollection<WorkTypeEntity>> SearchAsync(WorkTypeFilter filter, CancellationToken cancellationToken = default);

    Task<WorkTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task UpdateChecklistCodeAsync(string fromValue, string toValue, string? clientId, CancellationToken cancellationToken = default);

    Task UpdateDefaultPriorityAsync(string fromValue, string toValue, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithMaintenanceTypeAsync(string maintenanceTypeId, CancellationToken cancellationToken = default);

    Task UpdateMaintenanceTypeNameAsync(string maintenanceTypeId, string maintenanceTypeName, CancellationToken cancellationToken = default);
}

public sealed record WorkTypeFilter(
    string? Search,
    string? Status,
    string? MaintenanceTypeId,
    string? ClientId);
