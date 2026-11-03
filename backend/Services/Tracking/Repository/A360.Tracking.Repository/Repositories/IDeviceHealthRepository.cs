using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceHealthRepository : IMongoRepository<DeviceHealth>
{
    /// <summary>Each device has one health record, ignoring case.</summary>
    /// <remarks>excludeId is the health record being updated, so it does not clash with itself.</remarks>
    Task<bool> DeviceIdExistsAsync(string deviceId, string? excludeId = null, CancellationToken cancellationToken = default);
}
