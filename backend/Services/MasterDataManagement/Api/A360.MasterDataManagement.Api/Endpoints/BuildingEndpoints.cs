using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class BuildingEndpoints
{
    public static RouteGroupBuilder MapBuildingEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/buildings")
            .WithTags("Buildings");

        group.MapGet("", GetBuildingsAsync)
            .WithName("GetBuildings");

        group.MapGet("/{id}", GetBuildingByIdAsync)
            .WithName("GetBuildingById");

        group.MapPost("", CreateBuildingAsync)
            .WithName("CreateBuilding");

        group.MapPut("/{id}", UpdateBuildingAsync)
            .WithName("UpdateBuilding");

        group.MapPatch("/{id}/status", UpdateBuildingStatusAsync)
            .WithName("UpdateBuildingStatus");

        group.MapDelete("/{id}", DeleteBuildingAsync)
            .WithName("DeleteBuilding");

        return group;
    }

    private static async Task<IResult> GetBuildingsAsync(
        string? search,
        string? status,
        string? siteId,
        string? parentLevel,
        string? clientId,
        IBuildingRepository repository,
        CancellationToken cancellationToken)
    {
        var buildings = await repository.SearchAsync(
            new BuildingFilter(search, status, siteId, parentLevel, clientId),
            cancellationToken);

        return Results.Ok(
            buildings.Select(BuildingResponse.FromEntity));
    }

    private static async Task<IResult> GetBuildingByIdAsync(
        string id,
        IBuildingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid building id." });
        }

        var building = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (building is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Building",
            building.Id,
            building.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(BuildingResponse.FromEntity(building));
    }

    private static async Task<IResult> CreateBuildingAsync(
        CreateBuildingRequest request,
        IBuildingRepository repository,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var building = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            building.Code,
            building.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A building with code '{building.Code}' already exists." });
        }

        var siteError = await MasterDataReferenceResolver.ResolveSiteAsync(
            building.Site,
            sites,
            cancellationToken);

        if (siteError is not null)
        {
            return siteError;
        }

        building = await repository.CreateAsync(
            building,
            cancellationToken);

        await eventLogger.LogAsync(
            "Building",
            building.Id,
            building.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/buildings/{building.Id}",
            BuildingResponse.FromEntity(building));
    }

    private static async Task<IResult> UpdateBuildingAsync(
        string id,
        UpdateBuildingRequest request,
        IBuildingRepository repository,
        ISiteRepository sites,
        IFloorRepository floors,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid building id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var building = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (building is null)
        {
            return Results.NotFound();
        }

        var previousName = building.Name;

        request.ApplyTo(building);

        var existing = await repository.GetByCodeAsync(
            building.Code,
            building.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A building with code '{building.Code}' already exists." });
        }

        var siteError = await MasterDataReferenceResolver.ResolveSiteAsync(
            building.Site,
            sites,
            cancellationToken);

        if (siteError is not null)
        {
            return siteError;
        }

        var updated = await repository.UpdateAsync(
            id,
            building,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != building.Name)
        {
            await floors.UpdateBuildingNameAsync(
                id,
                building.Name,
                cancellationToken);

            await zones.UpdateBuildingNameAsync(
                id,
                building.Name,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Building",
            building.Id,
            building.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BuildingResponse.FromEntity(building));
    }

    private static async Task<IResult> UpdateBuildingStatusAsync(
        string id,
        UpdateBuildingStatusRequest request,
        IBuildingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid building id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var building = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (building is null)
        {
            return Results.NotFound();
        }

        building.Status = request.Status;
        building.UpdatedBy = request.UpdatedBy;
        building.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            building,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Building",
            building.Id,
            building.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BuildingResponse.FromEntity(building));
    }

    private static async Task<IResult> DeleteBuildingAsync(
        string id,
        IBuildingRepository repository,
        IFloorRepository floors,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid building id." });
        }

        var building = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (building is null)
        {
            return Results.NotFound();
        }

        if (await floors.AnyWithBuildingAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Floors still belong to this building. Delete or move them first." });
        }

        if (await zones.AnyWithBuildingAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Zones still belong to this building. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Building",
            building.Id,
            building.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
