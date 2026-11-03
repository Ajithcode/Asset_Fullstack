using A360.Domain.Entities;
using A360.Administration.Domain.Entities;
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

        group.MapGet("", GetLocationsAsync).WithName("GetOrganizationLocations");
        group.MapGet("/{locationId}", GetLocationByIdAsync).WithName("GetOrganizationLocationById");
        group.MapPost("", CreateLocationAsync).WithName("CreateOrganizationLocation");
        group.MapPut("/{locationId}", UpdateLocationAsync).WithName("UpdateOrganizationLocation");
        group.MapDelete("/{locationId}", DeleteLocationAsync).WithName("DeleteOrganizationLocation");

        return group;
    }

    private static async Task<IResult> GetLocationsAsync(
        string? tenantId,
        string? clientId,
        string? locationType,
        string? status,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var locations = await repository.GetAsync(tenantId, clientId, locationType, status, cancellationToken);

        // Parents filtered out of the list (by type or status) are fetched separately so every row still names its parent.
        var parents = locations.ToDictionary(location => location.LocationId);
        var missingParentIds = locations
            .Select(location => location.ParentLocationId)
            .OfType<string>()
            .Where(parentId => !parents.ContainsKey(parentId))
            .Distinct()
            .ToList();
        foreach (var parent in await repository.GetByLocationIdsAsync(missingParentIds, cancellationToken))
        {
            parents[parent.LocationId] = parent;
        }

        return Results.Ok(locations.Select(location => LocationResponse.FromEntity(
            location,
            location.ParentLocationId is null ? null : parents.GetValueOrDefault(location.ParentLocationId))));
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

        if (await repository.LocationCodeExistsAsync(location.LocationCode, location.TenantId, cancellationToken: cancellationToken))
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

    private static async Task<IResult> UpdateLocationAsync(
        string locationId,
        UpdateLocationRequest request,
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

        var location = await repository.GetByLocationIdAsync(locationId, cancellationToken);
        if (location is null)
        {
            return NotFound(locationId);
        }

        request.ApplyTo(location, currentUserAccessor.GetCurrentUser());

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

        if (parent is not null && await IsSelfOrDescendantAsync(parent, location.LocationId, repository, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParentLocationId)] = ["A location can't be moved under itself or one of its children"]
            });
        }

        if (await repository.LocationCodeExistsAsync(location.LocationCode, location.TenantId, location.LocationId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Location code '{location.LocationCode}' already exists" });
        }

        if (!await repository.UpdateAsync(location.Id, location, cancellationToken))
        {
            return NotFound(locationId);
        }

        await eventLogger.LogAsync(EntityType, location.Id, location.LocationName, EventAction.Updated, cancellationToken);

        return Results.Ok(LocationResponse.FromEntity(location, parent));
    }

    // Soft delete: the location stays in the collection with IsDeleted set.
    private static async Task<IResult> DeleteLocationAsync(
        string locationId,
        ILocationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var location = await repository.GetByLocationIdAsync(locationId, cancellationToken);
        if (location is null)
        {
            return NotFound(locationId);
        }

        if (await repository.HasChildrenAsync(location.LocationId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Location '{location.LocationName}' still has child locations. Move or delete them first." });
        }

        location.IsDeleted = true;
        location.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        location.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(location.Id, location, cancellationToken))
        {
            return NotFound(locationId);
        }

        await eventLogger.LogAsync(EntityType, location.Id, location.LocationName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // Walks up from the proposed parent; reaching the location itself means the move would create a cycle.
    private static async Task<bool> IsSelfOrDescendantAsync(
        Location candidate,
        string locationId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        Location? current = candidate;
        while (current is not null && visited.Add(current.LocationId))
        {
            if (current.LocationId == locationId)
            {
                return true;
            }

            current = current.ParentLocationId is null
                ? null
                : await repository.GetByLocationIdAsync(current.ParentLocationId, cancellationToken);
        }

        return false;
    }

    private static IResult NotFound(string locationId)
    {
        return Results.NotFound(new { message = $"Location '{locationId}' was not found." });
    }
}
