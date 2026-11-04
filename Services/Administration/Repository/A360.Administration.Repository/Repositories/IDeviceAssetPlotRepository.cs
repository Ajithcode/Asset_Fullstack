using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IDeviceAssetPlotRepository : IMongoRepository<DeviceAssetPlot>
{
    Task<DeviceAssetPlot?> GetByPlotIdAsync(string plotId, CancellationToken cancellationToken = default);

    /// <summary>True when the asset or device already has a plot (an item is plotted once).</summary>
    Task<bool> ItemPlottedAsync(string itemType, string itemId, string? tenantId, CancellationToken cancellationToken = default);
}
