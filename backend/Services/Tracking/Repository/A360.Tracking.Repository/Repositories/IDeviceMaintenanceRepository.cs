using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceMaintenanceRepository : IMongoRepository<DeviceMaintenance>
{
    Task<DeviceMaintenance?> GetByMaintenanceIdAsync(string maintenanceId, CancellationToken cancellationToken = default);

    /// <summary>A device cannot have the same type of maintenance twice on one due date, ignoring case.</summary>
    /// <remarks>excludeId is the job being updated, so it does not clash with itself.</remarks>
    Task<bool> ExistsAsync(string deviceId, string maintenanceType, DateTime dueDate, string? excludeId = null, CancellationToken cancellationToken = default);
}
