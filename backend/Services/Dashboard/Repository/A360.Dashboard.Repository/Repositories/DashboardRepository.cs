using MongoDB.Driver;
using A360.Repository.Repositories;
using DashboardEntity = A360.Dashboard.Domain.Entities.Dashboard;

namespace A360.Dashboard.Repository.Repositories;

public sealed class DashboardRepository : MongoRepository<DashboardEntity>, IDashboardRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "dashboards";

    public DashboardRepository(IMongoDatabase database)
        : base(database.GetCollection<DashboardEntity>(CollectionName))
    {
    }

    public async Task<DashboardEntity?> GetByDashboardIdAsync(string dashboardId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            return null;
        }

        return await Collection
            .Find(dashboard => dashboard.DashboardId == dashboardId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DashboardEntity>> GetAsync(
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<DashboardEntity>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(dashboard => dashboard.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(dashboard => dashboard.ClientId, clientId);
        }

        return await Collection
            .Find(filter)
            .SortByDescending(dashboard => dashboard.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DashboardEntity>(
                Builders<DashboardEntity>.IndexKeys.Ascending(dashboard => dashboard.DashboardId),
                new CreateIndexOptions { Name = "ux_dashboards_dashboard_id", Unique = true }),
            new CreateIndexModel<DashboardEntity>(
                Builders<DashboardEntity>.IndexKeys
                    .Ascending(dashboard => dashboard.TenantId)
                    .Ascending(dashboard => dashboard.ClientId),
                new CreateIndexOptions { Name = "ix_dashboards_tenant_id_client_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
