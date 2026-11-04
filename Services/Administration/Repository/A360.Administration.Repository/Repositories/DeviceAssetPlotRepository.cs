using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class DeviceAssetPlotRepository : MongoRepository<DeviceAssetPlot>, IDeviceAssetPlotRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_device_asset_plots";

    public DeviceAssetPlotRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceAssetPlot>(CollectionName))
    {
    }

    public async Task<DeviceAssetPlot?> GetByPlotIdAsync(string plotId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plotId))
        {
            return null;
        }

        return await Collection
            .Find(plot => plot.PlotId == plotId && !plot.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ItemPlottedAsync(
        string itemType,
        string itemId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(plot => plot.ItemType == itemType && plot.ItemId == itemId && plot.TenantId == tenantId && !plot.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DeviceAssetPlot>(
                Builders<DeviceAssetPlot>.IndexKeys.Ascending(plot => plot.PlotId),
                new CreateIndexOptions { Name = "ux_administration_device_asset_plots_plot_id", Unique = true }),
            new CreateIndexModel<DeviceAssetPlot>(
                Builders<DeviceAssetPlot>.IndexKeys
                    .Ascending(plot => plot.TenantId)
                    .Ascending(plot => plot.ItemType)
                    .Ascending(plot => plot.ItemId),
                new CreateIndexOptions { Name = "ix_administration_device_asset_plots_tenant_id_item" }),
            new CreateIndexModel<DeviceAssetPlot>(
                Builders<DeviceAssetPlot>.IndexKeys.Ascending(plot => plot.FloorPlanId),
                new CreateIndexOptions { Name = "ix_administration_device_asset_plots_floor_plan_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
