using MongoDB.Driver;
using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Repository.Repositories;

// Same document shape as the widgets collection; the data is maintained directly in the database.
public sealed class AddWidgetRepository : IAddWidgetRepository
{
    public const string CollectionName = "addwidget";

    private readonly IMongoCollection<WidgetEntity> _collection;

    public AddWidgetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<WidgetEntity>(CollectionName);
    }

    public async Task<WidgetEntity?> GetByWidgetIdAsync(string widgetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(widgetId))
        {
            return null;
        }

        return await _collection
            .Find(widget => widget.WidgetId == widgetId.Trim())
            .FirstOrDefaultAsync(cancellationToken);
    }
}
