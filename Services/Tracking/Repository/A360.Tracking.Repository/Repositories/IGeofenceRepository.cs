using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IGeofenceRepository : IMongoRepository<Geofence>
{
    /// <summary>Geofence names are unique within a site, ignoring case.</summary>
    /// <remarks>excludeId is the geofence being updated, so it does not clash with itself.</remarks>
    Task<bool> NameExistsAsync(string siteName, string name, string? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Adds one to the Rules column of the Geofence List.</summary>
    Task IncrementRuleCountAsync(string geofenceId, CancellationToken cancellationToken = default);

    /// <summary>Takes one off the Rules column of the Geofence List, never below zero.</summary>
    Task DecrementRuleCountAsync(string geofenceId, CancellationToken cancellationToken = default);
}
