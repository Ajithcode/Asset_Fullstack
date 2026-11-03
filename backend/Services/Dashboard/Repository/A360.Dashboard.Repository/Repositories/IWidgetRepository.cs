using A360.Repository.Repositories;
using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Repository.Repositories;

public interface IWidgetRepository : IMongoRepository<WidgetEntity>
{
    Task<WidgetEntity?> GetByWidgetIdAsync(string widgetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WidgetEntity>> GetAsync(
        string? dashboardId,
        string? tenantId,
        string? clientId,
        string? moduleName,
        CancellationToken cancellationToken = default);
}
