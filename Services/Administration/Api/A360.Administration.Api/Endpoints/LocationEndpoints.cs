using A360.Domain.Entities;
using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class LocationEndpoints
{
    private const string EntityType = "OrganizationLocation";

    public static RouteGroupBuilder MapLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        // Prefixed with /organization because the Locating service already owns /api/locations at the gateway.
        var group = routes.MapGroup("/api/organization/locations").WithTags("Organization Locations");

        group.MapGet("/{locationId}", GetLocationByIdAsync).WithName("GetOrganizationLocationById");
        group.MapPost("", CreateLocationAsync).WithName("CreateOrganizationLocation");

        return group;
    }

    private static async Task<IResult> GetLocationByIdAsync(
        string locationId,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var location = await repository.GetByLocationIdAsync(locationId, cancellationToken);
        if (location is null)
        {
            return Results.NotFound();
        }

        var parent = location.ParentLocationId is null
            ? null
            : await repository.GetByLocationIdAsync(location.ParentLocationId, cancellationToken);

        await eventLogger.LogAsync(EntityType, location.Id, location.LocationName, EventAction.Viewed, cancellationToken);

        return Results.Ok(LocationResponse.FromEntity(location, parent));
    }

    private static async Task<IResult> CreateLocationAsync(
        CreateLocationRequest request,
        ILocationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var location = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.LocationCodeExistsAsync(location.LocationCode, location.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Location code '{location.LocationCode}' already exists" });
        }

        var parent = location.ParentLocationId is null
            ? null
            : await repository.GetByLocationIdAsync(location.ParentLocationId, cancellationToken);
        if (location.ParentLocationId is not null && parent is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParentLocationId)] = [$"Parent location '{location.ParentLocationId}' was not found"]
            });
        }

        location = await repository.CreateAsync(location, cancellationToken);

        await eventLogger.LogAsync(EntityType, location.Id, location.LocationName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/organization/locations/{location.LocationId}",
            LocationResponse.FromEntity(location, parent));
    }
}
