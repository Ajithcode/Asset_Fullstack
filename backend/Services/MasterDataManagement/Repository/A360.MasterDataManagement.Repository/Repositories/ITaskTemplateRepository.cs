using TaskTemplateEntity = A360.MasterDataManagement.Domain.Entities.TaskTemplate;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ITaskTemplateRepository : IMongoRepository<TaskTemplateEntity>
{
    Task<IReadOnlyCollection<TaskTemplateEntity>> SearchAsync(TaskTemplateFilter filter, CancellationToken cancellationToken = default);

    Task<TaskTemplateEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithMaintenanceTypeAsync(string maintenanceTypeId, CancellationToken cancellationToken = default);

    Task UpdateMaintenanceTypeNameAsync(string maintenanceTypeId, string maintenanceTypeName, CancellationToken cancellationToken = default);
}

public sealed record TaskTemplateFilter(
    string? Search,
    string? Status,
    string? MaintenanceTypeId,
    string? ClientId);
