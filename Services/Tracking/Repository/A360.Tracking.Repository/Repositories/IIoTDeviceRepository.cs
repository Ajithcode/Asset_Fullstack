using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IIoTDeviceRepository : IMongoRepository<IoTDevice>
{
    Task<IoTDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Device ids are unique, ignoring case.</summary>
    Task<bool> DeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default);
}
