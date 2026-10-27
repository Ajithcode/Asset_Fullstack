using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceHealthRepository : IMongoRepository<DeviceHealth>
{
    /// <summary>Each device has one health record, ignoring case.</summary>
    Task<bool> DeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default);
}
