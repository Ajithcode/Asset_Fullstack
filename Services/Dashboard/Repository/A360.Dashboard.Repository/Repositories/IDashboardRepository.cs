using A360.Repository.Repositories;
using DashboardEntity = A360.Dashboard.Domain.Entities.Dashboard;

namespace A360.Dashboard.Repository.Repositories;

public interface IDashboardRepository : IMongoRepository<DashboardEntity>
{
    Task<DashboardEntity?> GetByDashboardIdAsync(string dashboardId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DashboardEntity>> GetAsync(string? tenantId, string? clientId, CancellationToken cancellationToken = default);
}
