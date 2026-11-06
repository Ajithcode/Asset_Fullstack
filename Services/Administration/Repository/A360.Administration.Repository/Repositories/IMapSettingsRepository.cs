using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapSettingsRepository : IMongoRepository<MapSettings>
{
    /// <summary>The settings of a tenant/client, or null when none were saved.</summary>
    Task<MapSettings?> GetByScopeAsync(string? clientId, string? tenantId, CancellationToken cancellationToken = default);
}
