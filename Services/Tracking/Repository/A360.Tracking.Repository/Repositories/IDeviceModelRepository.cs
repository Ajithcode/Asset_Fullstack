using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceModelRepository : IMongoRepository<DeviceModel>
{
    Task<DeviceModel?> GetByModelNumberAsync(string modelNumber, CancellationToken cancellationToken = default);

    /// <summary>Model numbers are unique, ignoring case.</summary>
    Task<bool> ModelNumberExistsAsync(string modelNumber, CancellationToken cancellationToken = default);

    /// <summary>Number of non-deleted models per device type, as stored.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByDeviceTypeAsync(CancellationToken cancellationToken = default);

    /// <summary>Models that can be picked for a new device (not deleted or Inactive), optionally of one device type ignoring case.</summary>
    Task<IReadOnlyList<DeviceModel>> GetSelectableAsync(string? deviceType, CancellationToken cancellationToken = default);
}
