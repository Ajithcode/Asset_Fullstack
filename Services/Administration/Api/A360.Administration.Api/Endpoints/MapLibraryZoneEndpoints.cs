using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>Map Library (Administration > Organization &amp; Locations > Maps &amp; Floor Plans): zones drawn on floor plans.</summary>
public static class MapLibraryZoneEndpoints
{
    private const string EntityType = "MapLibraryZone";
    private const string Route = "/api/map-library/zones";

    public static RouteGroupBuilder MapMapLibraryZoneEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("Map Library");

        group.MapGet("/options", () => Results.Ok(MapLibraryZoneOptionsResponse.Instance)).WithName("GetMapLibraryZoneOptions");
        group.MapGet("/{zoneId}", GetZoneByIdAsync).WithName("GetMapLibraryZoneById");
        group.MapPost("", CreateZoneAsync).WithName("CreateMapLibraryZone");

        return group;
    }

    private static async Task<IResult> GetZoneByIdAsync(
        string zoneId,
        IMapLibraryZoneRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var zone = await repository.GetByZoneIdAsync(zoneId, cancellationToken);
        if (zone is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, zone.Id, zone.ZoneName, EventAction.Viewed, cancellationToken);

        return Results.Ok(MapLibraryZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> CreateZoneAsync(
        CreateMapLibraryZoneRequest request,
        IMapLibraryZoneRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zone = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.ZoneCodeExistsAsync(zone.ZoneCode, zone.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Zone code '{zone.ZoneCode}' already exists" });
        }

        zone = await repository.CreateAsync(zone, cancellationToken);

        await eventLogger.LogAsync(EntityType, zone.Id, zone.ZoneName, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/{zone.ZoneId}", MapLibraryZoneResponse.FromEntity(zone));
    }
}
