using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapLayerSetRepository : IMongoRepository<MapLayerSet>
{
    Task<MapLayerSet?> GetByLayerSetIdAsync(string layerSetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MapLayerSet>> GetAsync(string? clientId, string? tenantId, CancellationToken cancellationToken = default);

    /// <summary>True when a layer set (not deleted) with this name, case-insensitive, exists for the tenant/client.</summary>
    Task<bool> NameExistsAsync(string name, string? clientId, string? tenantId, CancellationToken cancellationToken = default);
}
