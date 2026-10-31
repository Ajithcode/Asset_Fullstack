using MongoDB.Driver;
using A360.Repository.Repositories;
using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Repository.Repositories;

public sealed class WidgetRepository : MongoRepository<WidgetEntity>, IWidgetRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "widgets";

    public WidgetRepository(IMongoDatabase database)
        : base(database.GetCollection<WidgetEntity>(CollectionName))
    {
    }

    public async Task<WidgetEntity?> GetByWidgetIdAsync(string widgetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(widgetId))
        {
            return null;
        }

        return await Collection
            .Find(widget => widget.WidgetId == widgetId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<WidgetEntity>> GetAsync(
        string? dashboardId,
        string? tenantId,
        string? clientId,
        string? moduleName,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<WidgetEntity>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(dashboardId))
        {
            filter &= builder.Eq(widget => widget.DashboardId, dashboardId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(widget => widget.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(widget => widget.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            filter &= builder.Eq(widget => widget.ModuleName, moduleName.Trim());
        }

        return await Collection
            .Find(filter)
            .SortByDescending(widget => widget.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WidgetEntity>(
                Builders<WidgetEntity>.IndexKeys.Ascending(widget => widget.WidgetId),
                new CreateIndexOptions { Name = "ux_widgets_widget_id", Unique = true }),
            new CreateIndexModel<WidgetEntity>(
                Builders<WidgetEntity>.IndexKeys.Ascending(widget => widget.DashboardId),
                new CreateIndexOptions { Name = "ix_widgets_dashboard_id" }),
            new CreateIndexModel<WidgetEntity>(
                Builders<WidgetEntity>.IndexKeys
                    .Ascending(widget => widget.TenantId)
                    .Ascending(widget => widget.ClientId)
                    .Ascending(widget => widget.ModuleName),
                new CreateIndexOptions { Name = "ix_widgets_tenant_id_client_id_module_name" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
