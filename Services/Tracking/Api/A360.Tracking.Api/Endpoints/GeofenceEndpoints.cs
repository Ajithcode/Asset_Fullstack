using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Geofence & Alerts → Geofence List, addressed by geofence id.</summary>
public static class GeofenceEndpoints
{
    private const string EntityType = "Geofence";

    public static RouteGroupBuilder MapGeofenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/geofences").WithTags("Geofences");

        group.MapGet("", GetGeofencesAsync).WithName("GetGeofences");
        group.MapGet("/{id}", GetGeofenceAsync).WithName("GetGeofenceById");
        group.MapPost("", CreateGeofenceAsync).WithName("CreateGeofence");
        group.MapPut("/{id}", UpdateGeofenceAsync).WithName("UpdateGeofence");
        group.MapDelete("/{id}", DeleteGeofenceAsync).WithName("DeleteGeofence");

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

    private static async Task<IResult> GetGeofencesAsync(
        IGeofenceRepository repository,
        CancellationToken cancellationToken)
    {
        var geofences = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(geofences).Select(GeofenceResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetGeofenceAsync(
        string id,
        IGeofenceRepository repository,
        CancellationToken cancellationToken)
    {
        var geofence = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        return geofence is null || geofence.IsDeleted
            ? NotFound(id)
            : Results.Ok(GeofenceResponse.FromEntity(geofence));
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

        if (await repository.NameExistsAsync(geofence.SiteName, geofence.Name, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Geofence '{geofence.Name}' already exists at {geofence.SiteName}." });
        }

        await repository.CreateAsync(geofence, cancellationToken);
        await eventLogger.LogAsync(EntityType, geofence.Id, $"{geofence.SiteName} - {geofence.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/geofences/{geofence.Id}", GeofenceResponse.FromEntity(geofence));
    }

    /// <summary>Takes the same body as create. The Rules count is kept, since alert rules maintain it.</summary>
    private static async Task<IResult> UpdateGeofenceAsync(
        string id,
        CreateGeofenceRequest request,
        IGeofenceRepository repository,
        IAlertRuleRepository alertRuleRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (existing is null || existing.IsDeleted)
        {
            return NotFound(id);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var geofence = request.ToEntity(currentUser).Replacing(existing, currentUser);
        geofence.RuleCount = existing.RuleCount;

        if (await repository.NameExistsAsync(geofence.SiteName, geofence.Name, existing.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Geofence '{geofence.Name}' already exists at {geofence.SiteName}." });
        }

        if (!await repository.UpdateAsync(existing.Id, geofence, cancellationToken))
        {
            return NotFound(id);
        }

        if (geofence.Name != existing.Name)
        {
            await alertRuleRepository.RenameGeofenceAsync(geofence.Id, geofence.Name, cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, geofence.Id, $"{geofence.SiteName} - {geofence.Name}", EventAction.Updated, cancellationToken);

        return Results.Ok(GeofenceResponse.FromEntity(geofence));
    }

    private static async Task<IResult> DeleteGeofenceAsync(
        string id,
        IGeofenceRepository repository,
        IAlertRuleRepository alertRuleRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var geofence = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (geofence is null || geofence.IsDeleted)
        {
            return NotFound(id);
        }

        if (await alertRuleRepository.AnyForGeofenceAsync(geofence.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Geofence '{geofence.Name}' has alert rules. Delete them first." });
        }

        if (!await repository.DeleteAsync(geofence.Id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, geofence.Id, $"{geofence.SiteName} - {geofence.Name}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Geofence '{id}' was not found." });
    }
}
