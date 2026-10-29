using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IAlertRuleRepository : IMongoRepository<AlertRule>
{
    /// <summary>Alert rule names are unique within a geofence, ignoring case.</summary>
    Task<bool> NameExistsAsync(string geofenceId, string name, CancellationToken cancellationToken = default);
}
