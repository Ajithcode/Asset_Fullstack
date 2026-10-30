using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceConfigurationRepository : IMongoRepository<DeviceConfiguration>
{
    /// <summary>All non-deleted versions of a device, newest first.</summary>
    Task<IReadOnlyList<DeviceConfiguration>> GetVersionsAsync(string deviceId, CancellationToken cancellationToken = default);

    Task<DeviceConfiguration?> GetVersionAsync(string deviceId, int version, CancellationToken cancellationToken = default);
}
