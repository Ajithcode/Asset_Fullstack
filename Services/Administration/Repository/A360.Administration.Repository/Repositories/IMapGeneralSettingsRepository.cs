using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapGeneralSettingsRepository : IMongoRepository<MapGeneralSettings>
{
    Task<MapGeneralSettings?> GetBySettingsIdAsync(string settingsId, CancellationToken cancellationToken = default);

    Task<MapGeneralSettings?> GetByScopeAsync(string? tenantId, string? clientId, CancellationToken cancellationToken = default);
}
