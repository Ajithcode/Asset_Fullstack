using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IFirmwareVersionRepository : IMongoRepository<FirmwareVersion>
{
    /// <summary>Each device model has one firmware row, ignoring case.</summary>
    /// <remarks>excludeId is the firmware row being updated, so it does not clash with itself.</remarks>
    Task<bool> DeviceModelExistsAsync(string deviceModel, string? excludeId = null, CancellationToken cancellationToken = default);
}
