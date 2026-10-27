using MongoDB.Driver;
using A360.Project.Domain.Entities;
using A360.Project.Repository.Repositories;

namespace A360.Locating.Repository.Repositories;

// Reads the existing project hierarchy collections (same database), filtered by their parent ids.
public sealed class LocationRepository : ILocationRepository
{
    private readonly IMongoDatabase _database;

    public LocationRepository(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyCollection<Country>> GetCountriesAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        return await _database
            .GetCollection<Country>(CountryRepository.CollectionName)
            .Find(country => country.ProjectId == projectId && !country.IsDeleted)
            .SortBy(country => country.CountryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Area>> GetAreasAsync(
        string projectId,
        string countryId,
        CancellationToken cancellationToken = default)
    {
        return await _database
            .GetCollection<Area>(AreaRepository.CollectionName)
            .Find(area =>
                area.ProjectId == projectId &&
                area.CountryId == countryId &&
                !area.IsDeleted)
            .SortBy(area => area.AreaName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Building>> GetBuildingsAsync(
        string projectId,
        string countryId,
        string areaId,
        CancellationToken cancellationToken = default)
    {
        return await _database
            .GetCollection<Building>(BuildingRepository.CollectionName)
            .Find(building =>
                building.ProjectId == projectId &&
                building.CountryId == countryId &&
                building.AreaId == areaId &&
                !building.IsDeleted)
            .SortBy(building => building.BuildingName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Floor>> GetFloorsAsync(
        string projectId,
        string countryId,
        string areaId,
        string buildingId,
        CancellationToken cancellationToken = default)
    {
        return await _database
            .GetCollection<Floor>(FloorRepository.CollectionName)
            .Find(floor =>
                floor.ProjectId == projectId &&
                floor.CountryId == countryId &&
                floor.AreaId == areaId &&
                floor.BuildingId == buildingId &&
                !floor.IsDeleted)
            .SortBy(floor => floor.FloorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Zone>> GetZonesAsync(
        string projectId,
        string countryId,
        string areaId,
        string buildingId,
        string floorId,
        CancellationToken cancellationToken = default)
    {
        return await _database
            .GetCollection<Zone>(ZoneRepository.CollectionName)
            .Find(zone =>
                zone.ProjectId == projectId &&
                zone.CountryId == countryId &&
                zone.AreaId == areaId &&
                zone.BuildingId == buildingId &&
                zone.FloorId == floorId &&
                !zone.IsDeleted)
            .SortBy(zone => zone.ZoneName)
            .ToListAsync(cancellationToken);
    }
}
