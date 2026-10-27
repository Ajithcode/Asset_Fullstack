using A360.Locating.Repository.Repositories;
using A360.Repository.Repositories;

namespace A360.Locating.Api.Endpoints;

public static class LocationEndpoints
{
    public static RouteGroupBuilder MapLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations").WithTags("Locations");

        group.MapGet("/countries", GetCountriesAsync).WithName("GetLocationCountries");
        group.MapGet("/areas", GetAreasAsync).WithName("GetLocationAreas");
        group.MapGet("/buildings", GetBuildingsAsync).WithName("GetLocationBuildings");
        group.MapGet("/floors", GetFloorsAsync).WithName("GetLocationFloors");
        group.MapGet("/zones", GetZonesAsync).WithName("GetLocationZones");

        return group;
    }

    private static async Task<IResult> GetCountriesAsync(
        string? projectId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateIds(("projectId", projectId));
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return Results.Ok(await repository.GetCountriesAsync(projectId!, cancellationToken));
    }

    private static async Task<IResult> GetAreasAsync(
        string? projectId,
        string? countryId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateIds(("projectId", projectId), ("countryId", countryId));
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return Results.Ok(await repository.GetAreasAsync(projectId!, countryId!, cancellationToken));
    }

    private static async Task<IResult> GetBuildingsAsync(
        string? projectId,
        string? countryId,
        string? areaId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateIds(("projectId", projectId), ("countryId", countryId), ("areaId", areaId));
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return Results.Ok(await repository.GetBuildingsAsync(projectId!, countryId!, areaId!, cancellationToken));
    }

    private static async Task<IResult> GetFloorsAsync(
        string? projectId,
        string? countryId,
        string? areaId,
        string? buildingId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateIds(
            ("projectId", projectId),
            ("countryId", countryId),
            ("areaId", areaId),
            ("buildingId", buildingId));
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return Results.Ok(await repository.GetFloorsAsync(projectId!, countryId!, areaId!, buildingId!, cancellationToken));
    }

    private static async Task<IResult> GetZonesAsync(
        string? projectId,
        string? countryId,
        string? areaId,
        string? buildingId,
        string? floorId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateIds(
            ("projectId", projectId),
            ("countryId", countryId),
            ("areaId", areaId),
            ("buildingId", buildingId),
            ("floorId", floorId));
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return Results.Ok(await repository.GetZonesAsync(projectId!, countryId!, areaId!, buildingId!, floorId!, cancellationToken));
    }

    private static Dictionary<string, string[]> ValidateIds(params (string Name, string? Value)[] ids)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var (name, value) in ids)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors[name] = [$"{name} is required."];
            }
            else if (!MongoObjectId.IsValid(value))
            {
                errors[name] = [$"{name} must be a valid id."];
            }
        }

        return errors;
    }
}
