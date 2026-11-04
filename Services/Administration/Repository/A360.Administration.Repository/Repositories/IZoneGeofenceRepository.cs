using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IZoneGeofenceRepository : IMongoRepository<ZoneGeofence>
{
    Task<ZoneGeofence?> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default);

    /// <summary>True when the zone code is already used in the tenant, ignoring case.</summary>
    Task<bool> ZoneCodeExistsAsync(string zoneCode, string? tenantId, CancellationToken cancellationToken = default);
}
