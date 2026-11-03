using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ISensorRepository : IMongoRepository<Sensor>
{
    Task<Sensor?> GetBySensorIdAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>Sensor ids are unique, ignoring case.</summary>
    Task<bool> SensorIdExistsAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>Serial numbers are unique, ignoring case; exceptId skips the sensor being updated.</summary>
    Task<bool> SerialNumberExistsAsync(string serialNumber, string? exceptId, CancellationToken cancellationToken = default);

    /// <summary>Non-deleted sensors on a device, ignoring case of the device id.</summary>
    Task<IReadOnlyList<Sensor>> GetByDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
}
