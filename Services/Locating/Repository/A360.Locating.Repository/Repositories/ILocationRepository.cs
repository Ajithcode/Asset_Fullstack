using A360.Project.Domain.Entities;

namespace A360.Locating.Repository.Repositories;

public interface ILocationRepository
{
    Task<IReadOnlyCollection<Country>> GetCountriesAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Area>> GetAreasAsync(
        string projectId,
        string countryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Building>> GetBuildingsAsync(
        string projectId,
        string countryId,
        string areaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Floor>> GetFloorsAsync(
        string projectId,
        string countryId,
        string areaId,
        string buildingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Zone>> GetZonesAsync(
        string projectId,
        string countryId,
        string areaId,
        string buildingId,
        string floorId,
        CancellationToken cancellationToken = default);
}
