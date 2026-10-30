using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IAlertRuleRepository : IMongoRepository<AlertRule>
{
    /// <summary>Alert rule names are unique within a geofence, ignoring case.</summary>
    /// <remarks>excludeId is the rule being updated, so it does not clash with itself.</remarks>
    Task<bool> NameExistsAsync(string geofenceId, string name, string? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Whether any alert rule watches the geofence.</summary>
    Task<bool> AnyForGeofenceAsync(string geofenceId, CancellationToken cancellationToken = default);

    /// <summary>Keeps the geofence name copied onto its rules in step after the geofence is renamed.</summary>
    Task RenameGeofenceAsync(string geofenceId, string geofenceName, CancellationToken cancellationToken = default);
}
