using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Repository.Repositories;

// Read-only: documents in this collection are inserted directly in the database, not through the API.
public interface IAddWidgetRepository
{
    Task<WidgetEntity?> GetByWidgetIdAsync(string widgetId, CancellationToken cancellationToken = default);
}
