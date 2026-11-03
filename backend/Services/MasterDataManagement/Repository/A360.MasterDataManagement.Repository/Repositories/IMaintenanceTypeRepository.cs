using MaintenanceTypeEntity = A360.MasterDataManagement.Domain.Entities.MaintenanceType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IMaintenanceTypeRepository : IMongoRepository<MaintenanceTypeEntity>
{
    Task<IReadOnlyCollection<MaintenanceTypeEntity>> SearchAsync(MaintenanceTypeFilter filter, CancellationToken cancellationToken = default);

    Task<MaintenanceTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task UpdateDefaultPriorityAsync(string fromValue, string toValue, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record MaintenanceTypeFilter(
    string? Search,
    string? Status,
    string? ClientId);
