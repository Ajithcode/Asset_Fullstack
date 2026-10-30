using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Geofence & Alerts → Geofence List.</summary>
public static class GeofenceEndpoints
{
    private const string EntityType = "Geofence";

    public static RouteGroupBuilder MapGeofenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/geofences").WithTags("Geofences");

        group.MapPost("", CreateGeofenceAsync).WithName("CreateGeofence");

        // Create Geofence dropdowns. Site comes from GET /api/master-data/sites.
        group.MapGet("/options", GetOptionsAsync).WithName("GetGeofenceOptions");
        group.MapGet("/zone-types", () => Results.Ok(GeofenceType.All)).WithName("GetGeofenceZoneTypes");
        group.MapGet("/map-colours", () => Results.Ok(GeofenceMapColour.All)).WithName("GetGeofenceMapColours");
        group.MapGet("/asset-groups", GetAssetGroupsAsync).WithName("GetGeofenceAssetGroups");
        group.MapGet("/schedules", () => Results.Ok(GeofenceSchedule.All)).WithName("GetGeofenceSchedules");
        group.MapGet("/shapes", () => Results.Ok(GeofenceShape.All)).WithName("GetGeofenceShapes");
        group.MapGet("/statuses", () => Results.Ok(GeofenceStatus.All)).WithName("GetGeofenceStatuses");

        return group;
    }

    private static async Task<IResult> GetOptionsAsync(IGeofenceRepository repository, CancellationToken cancellationToken)
    {
        return Results.Ok(new GeofenceOptionsResponse(
            GeofenceType.All,
            GeofenceMapColour.All,
            await LoadAssetGroupsAsync(repository, cancellationToken),
            GeofenceSchedule.All,
            GeofenceShape.All,
            GeofenceStatus.All,
            GeofenceShape.DefaultPolygonPoints,
            GeofenceShape.MinPolygonPoints,
            GeofenceShape.MaxPolygonPoints));
    }

    private static async Task<IResult> GetAssetGroupsAsync(IGeofenceRepository repository, CancellationToken cancellationToken)
    {
        return Results.Ok(await LoadAssetGroupsAsync(repository, cancellationToken));
    }

    /// <summary>The catalog groups, then any other group already used by a geofence, with "All assets" last.</summary>
    private static async Task<IReadOnlyList<string>> LoadAssetGroupsAsync(IGeofenceRepository repository, CancellationToken cancellationToken)
    {
        var inUse = await repository.GetAssetGroupsInUseAsync(cancellationToken);
        var custom = inUse
            .Where(group => ContractText.Canonical(group, GeofenceAssetGroup.All) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase);

        return GeofenceAssetGroup.All
            .Where(group => group != GeofenceAssetGroup.AllAssets)
            .Concat(custom)
            .Append(GeofenceAssetGroup.AllAssets)
            .ToList();
    }

    private static async Task<IResult> CreateGeofenceAsync(
        CreateGeofenceRequest request,
        IGeofenceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var geofence = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.NameExistsAsync(geofence.SiteName, geofence.Name, cancellationToken))
        {
            return Results.Conflict(new { message = $"Geofence '{geofence.Name}' already exists at {geofence.SiteName}." });
        }

        await repository.CreateAsync(geofence, cancellationToken);
        await eventLogger.LogAsync(EntityType, geofence.Id, $"{geofence.SiteName} - {geofence.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/geofences/{geofence.Id}", GeofenceResponse.FromEntity(geofence));
    }
}
