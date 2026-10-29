using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IGeofenceRepository : IMongoRepository<Geofence>
{
    /// <summary>Geofence names are unique within a site, ignoring case.</summary>
    Task<bool> NameExistsAsync(string siteName, string name, CancellationToken cancellationToken = default);

    /// <summary>Adds one to the Rules column of the Geofence List.</summary>
    Task IncrementRuleCountAsync(string geofenceId, CancellationToken cancellationToken = default);
}
