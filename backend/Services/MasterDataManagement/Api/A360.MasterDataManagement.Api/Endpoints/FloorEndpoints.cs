using FloorEntity = A360.MasterDataManagement.Domain.Entities.Floor;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class FloorEndpoints
{
    public static RouteGroupBuilder MapFloorEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/floors")
            .WithTags("Floors");

        group.MapGet("", GetFloorsAsync)
            .WithName("GetFloors");

        group.MapGet("/{id}", GetFloorByIdAsync)
            .WithName("GetFloorById");

        group.MapPost("", CreateFloorAsync)
            .WithName("CreateFloor");

        group.MapPut("/{id}", UpdateFloorAsync)
            .WithName("UpdateFloor");

        group.MapPatch("/{id}/status", UpdateFloorStatusAsync)
            .WithName("UpdateFloorStatus");

        group.MapDelete("/{id}", DeleteFloorAsync)
            .WithName("DeleteFloor");

        return group;
    }

    private static async Task<IResult> GetFloorsAsync(
        string? search,
        string? status,
        string? siteId,
        string? buildingId,
        string? parentLevel,
        string? clientId,
        IFloorRepository repository,
        CancellationToken cancellationToken)
    {
        var floors = await repository.SearchAsync(
            new FloorFilter(search, status, siteId, buildingId, parentLevel, clientId),
            cancellationToken);

        return Results.Ok(
            floors.Select(FloorResponse.FromEntity));
    }

    private static async Task<IResult> GetFloorByIdAsync(
        string id,
        IFloorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid floor id." });
        }

        var floor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (floor is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Floor",
            floor.Id,
            floor.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(FloorResponse.FromEntity(floor));
    }

    private static async Task<IResult> CreateFloorAsync(
        CreateFloorRequest request,
        IFloorRepository repository,
        IBuildingRepository buildings,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var floor = request.ToEntity();

        var referenceError = await ResolveReferencesAsync(
            floor,
            buildings,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            floor.Code,
            floor.BuildingId,
            floor.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"Building '{floor.BuildingName}' already has a floor with code '{floor.Code}'." });
        }

        floor = await repository.CreateAsync(
            floor,
            cancellationToken);

        await eventLogger.LogAsync(
            "Floor",
            floor.Id,
            floor.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/floors/{floor.Id}",
            FloorResponse.FromEntity(floor));
    }

    private static async Task<IResult> UpdateFloorAsync(
        string id,
        UpdateFloorRequest request,
        IFloorRepository repository,
        IBuildingRepository buildings,
        ISiteRepository sites,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid floor id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var floor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (floor is null)
        {
            return Results.NotFound();
        }

        var previousName = floor.Name;
        var previousBuildingId = floor.BuildingId;

        request.ApplyTo(floor);

        var referenceError = await ResolveReferencesAsync(
            floor,
            buildings,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            floor.Code,
            floor.BuildingId,
            floor.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"Building '{floor.BuildingName}' already has a floor with code '{floor.Code}'." });
        }

        var updated = await repository.UpdateAsync(
            id,
            floor,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != floor.Name)
        {
            await zones.UpdateFloorNameAsync(
                id,
                floor.Name,
                cancellationToken);
        }

        // Zones on this floor move with it.
        if (previousBuildingId != floor.BuildingId)
        {
            await zones.UpdateFloorBuildingAsync(
                id,
                floor.BuildingId,
                floor.BuildingName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Floor",
            floor.Id,
            floor.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(FloorResponse.FromEntity(floor));
    }

    private static async Task<IResult> UpdateFloorStatusAsync(
        string id,
        UpdateFloorStatusRequest request,
        IFloorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid floor id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var floor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (floor is null)
        {
            return Results.NotFound();
        }

        floor.Status = request.Status;
        floor.UpdatedBy = request.UpdatedBy;
        floor.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            floor,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Floor",
            floor.Id,
            floor.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(FloorResponse.FromEntity(floor));
    }

    private static async Task<IResult> DeleteFloorAsync(
        string id,
        IFloorRepository repository,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid floor id." });
        }

        var floor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (floor is null)
        {
            return Results.NotFound();
        }

        if (await zones.AnyWithFloorAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Zones still belong to this floor. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Floor",
            floor.Id,
            floor.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the building exists and copies its name onto the floor, then resolves the site against the
    /// building's site. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        FloorEntity floor,
        IBuildingRepository buildings,
        ISiteRepository sites,
        CancellationToken cancellationToken)
    {
        var building = MongoObjectId.IsValid(floor.BuildingId)
            ? await buildings.GetByIdAsync(floor.BuildingId, cancellationToken)
            : null;

        if (building is null)
        {
            return Results.BadRequest(
                new { message = $"Building '{floor.BuildingId}' was not found." });
        }

        floor.BuildingName = building.Name;

        var (site, siteError) = await MasterDataReferenceResolver.ResolveSiteFromParentAsync(
            floor.Site,
            building.Site,
            $"building '{building.Name}'",
            sites,
            cancellationToken);

        floor.Site = site;
        return siteError;
    }
}
