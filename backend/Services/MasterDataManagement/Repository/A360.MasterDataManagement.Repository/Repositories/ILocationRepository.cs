using LocationEntity = A360.MasterDataManagement.Domain.Entities.Location;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ILocationRepository : IMongoRepository<LocationEntity>
{
    Task<IReadOnlyCollection<LocationEntity>> SearchAsync(LocationFilter filter, CancellationToken cancellationToken = default);

    Task<LocationEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LocationEntity>> GetChildrenAsync(string parentLocationId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string parentLocationId, CancellationToken cancellationToken = default);

    Task UpdateParentLocationNameAsync(string parentLocationId, string parentLocationName, CancellationToken cancellationToken = default);
}

public sealed record LocationFilter(
    string? Search,
    string? Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? Status,
    string? ParentLocationId,
    string? ClientId);
