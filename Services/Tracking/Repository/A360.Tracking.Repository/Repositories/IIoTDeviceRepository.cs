using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IIoTDeviceRepository : IMongoRepository<IoTDevice>
{
    Task<IoTDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Device ids are unique, ignoring case.</summary>
    Task<bool> DeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Non-deleted devices whose id or name contains the search text, ignoring case, ordered by id.</summary>
    Task<IReadOnlyList<IoTDevice>> SearchAsync(string? search, int limit, CancellationToken cancellationToken = default);

    /// <summary>Devices of type <see cref="IoTDeviceType.Gateway"/>, ignoring case, that other devices can report through.</summary>
    Task<IReadOnlyList<IoTDevice>> GetGatewaysAsync(CancellationToken cancellationToken = default);
}
