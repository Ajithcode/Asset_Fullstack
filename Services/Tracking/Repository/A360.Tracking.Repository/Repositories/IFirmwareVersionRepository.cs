using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IFirmwareVersionRepository : IMongoRepository<FirmwareVersion>
{
    /// <summary>Each device model has one firmware row, ignoring case.</summary>
    Task<FirmwareVersion?> GetByDeviceModelAsync(string deviceModel, CancellationToken cancellationToken = default);

    Task<bool> DeviceModelExistsAsync(string deviceModel, CancellationToken cancellationToken = default);
}
