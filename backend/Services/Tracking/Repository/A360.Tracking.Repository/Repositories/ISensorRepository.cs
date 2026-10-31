using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ISensorRepository : IMongoRepository<Sensor>
{
    Task<Sensor?> GetBySensorIdAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>Sensor ids are unique, ignoring case.</summary>
    Task<bool> SensorIdExistsAsync(string sensorId, CancellationToken cancellationToken = default);
}
