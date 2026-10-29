using LocationEntity = A360.MasterDataManagement.Domain.Entities.Location;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class LocationEndpoints
{
    // Guards the parent-chain walk against corrupt data that already contains a loop.
    private const int MaxHierarchyDepth = 100;

    public static RouteGroupBuilder MapLocationEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/locations")
            .WithTags("Locations");

        group.MapGet("", GetLocationsAsync)
            .WithName("GetLocations");

        group.MapGet("/{id}", GetLocationByIdAsync)
            .WithName("GetLocationById");

        group.MapGet("/{id}/children", GetChildLocationsAsync)
            .WithName("GetChildLocations");

        group.MapPost("", CreateLocationAsync)
            .WithName("CreateLocation");

        group.MapPut("/{id}", UpdateLocationAsync)
            .WithName("UpdateLocation");

        group.MapPatch("/{id}/status", UpdateLocationStatusAsync)
            .WithName("UpdateLocationStatus");

        group.MapDelete("/{id}", DeleteLocationAsync)
            .WithName("DeleteLocation");

        return group;
    }

    private static async Task<IResult> GetLocationsAsync(
        string? search,
        string? type,
        string? building,
        string? floor,
        string? zone,
        string? status,
        string? parentLocationId,
        string? clientId,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        var locations = await repository.SearchAsync(
            new LocationFilter(search, type, building, floor, zone, status, parentLocationId, clientId),
            cancellationToken);

        return Results.Ok(
            locations.Select(LocationResponse.FromEntity));
    }

    private static async Task<IResult> GetLocationByIdAsync(
        string id,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var location = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Location",
            location.Id,
            location.LocationName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(LocationResponse.FromEntity(location));
    }

    private static async Task<IResult> GetChildLocationsAsync(
        string id,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var children = await repository.GetChildrenAsync(
            id,
            cancellationToken);

        return Results.Ok(
            children.Select(LocationResponse.FromEntity));
    }

    private static async Task<IResult> CreateLocationAsync(
        CreateLocationRequest request,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var location = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            location.Code,
            location.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A location with code '{location.Code}' already exists." });
        }

        var parentError = await ResolveParentAsync(
            location,
            repository,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        location = await repository.CreateAsync(
            location,
            cancellationToken);

        await eventLogger.LogAsync(
            "Location",
            location.Id,
            location.LocationName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/locations/{location.Id}",
            LocationResponse.FromEntity(location));
    }

    private static async Task<IResult> UpdateLocationAsync(
        string id,
        UpdateLocationRequest request,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var location = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        var previousName = location.LocationName;

        request.ApplyTo(location);

        var existing = await repository.GetByCodeAsync(
            location.Code,
            location.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A location with code '{location.Code}' already exists." });
        }

        var parentError = await ResolveParentAsync(
            location,
            repository,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        var updated = await repository.UpdateAsync(
            id,
            location,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != location.LocationName)
        {
            await repository.UpdateParentLocationNameAsync(
                id,
                location.LocationName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Location",
            location.Id,
            location.LocationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(LocationResponse.FromEntity(location));
    }

    private static async Task<IResult> UpdateLocationStatusAsync(
        string id,
        UpdateLocationStatusRequest request,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var location = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        location.Status = request.Status;
        location.UpdatedBy = request.UpdatedBy;
        location.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            location,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Location",
            location.Id,
            location.LocationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(LocationResponse.FromEntity(location));
    }

    private static async Task<IResult> DeleteLocationAsync(
        string id,
        ILocationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var location = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        if (await repository.HasChildrenAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This location has child locations. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Location",
            location.Id,
            location.LocationName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the parent exists and would not create a loop, then copies its name onto the location.
    /// Returns an error result, or null when the parent is valid (or not set).
    /// </summary>
    private static async Task<IResult?> ResolveParentAsync(
        LocationEntity location,
        ILocationRepository repository,
        CancellationToken cancellationToken)
    {
        if (location.ParentLocationId is null)
        {
            location.ParentLocationName = null;
            return null;
        }

        if (location.ParentLocationId == location.Id)
        {
            return Results.BadRequest(
                new { message = "A location cannot be its own parent." });
        }

        var parent = await repository.GetByIdAsync(
            location.ParentLocationId,
            cancellationToken);

        if (parent is null)
        {
            return Results.BadRequest(
                new { message = "Parent location was not found." });
        }

        // New locations have no id yet, so they cannot appear in any existing chain.
        if (!string.IsNullOrEmpty(location.Id))
        {
            var ancestor = parent;

            for (var depth = 0; ancestor?.ParentLocationId is not null && depth < MaxHierarchyDepth; depth++)
            {
                if (ancestor.ParentLocationId == location.Id)
                {
                    return Results.BadRequest(
                        new { message = "Parent location cannot be one of this location's own children." });
                }

                ancestor = await repository.GetByIdAsync(
                    ancestor.ParentLocationId,
                    cancellationToken);
            }
        }

        location.ParentLocationName = parent.LocationName;
        return null;
    }
}
