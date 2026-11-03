using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceModelRepository : IMongoRepository<DeviceModel>
{
    Task<DeviceModel?> GetByModelNumberAsync(string modelNumber, CancellationToken cancellationToken = default);

    /// <summary>Model numbers are unique, ignoring case.</summary>
    Task<bool> ModelNumberExistsAsync(string modelNumber, CancellationToken cancellationToken = default);
}
