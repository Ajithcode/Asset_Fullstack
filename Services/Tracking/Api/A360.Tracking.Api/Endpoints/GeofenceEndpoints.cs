using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
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

        return group;
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
