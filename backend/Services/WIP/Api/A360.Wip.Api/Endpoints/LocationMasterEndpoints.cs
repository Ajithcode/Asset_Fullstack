
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class LocationMasterEndpoints
{
    public static RouteGroupBuilder MapLocationMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/location-masters")
            .WithTags("LocationMasters");

        group.MapGet("", GetLocationMastersAsync)
            .WithName("GetLocationMasters");

        group.MapGet("/{id}", GetLocationMasterByIdAsync)
            .WithName("GetLocationMasterById");

        group.MapGet("/asset/{assetId}", GetLocationMastersByAssetIdAsync)
            .WithName("GetLocationMastersByAssetId");

        group.MapPost("", CreateLocationMasterAsync)
            .WithName("CreateLocationMaster");

        group.MapPut("/{id}", UpdateLocationMasterAsync)
            .WithName("UpdateLocationMaster");

        group.MapDelete("/{id}", DeleteLocationMasterAsync)
            .WithName("DeleteLocationMaster");

        return group;
    }

    private static async Task<IResult> GetLocationMastersAsync(
        ILocationMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var locationMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            locationMasters.Select(LocationMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetLocationMasterByIdAsync(
        string id,
        ILocationMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var locationMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (locationMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "LocationMaster",
            locationMaster.Id,
            locationMaster.LocationId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(LocationMasterResponse.FromEntity(locationMaster));
    }

    private static async Task<IResult> GetLocationMastersByAssetIdAsync(
        string assetId,
        ILocationMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var locationMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            locationMasters.Select(LocationMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateLocationMasterAsync(
        CreateLocationMasterRequest request,
        ILocationMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var locationMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "LocationMaster",
            locationMaster.Id,
            locationMaster.LocationId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/location-masters/{locationMaster.Id}",
            LocationMasterResponse.FromEntity(locationMaster));
    }

    private static async Task<IResult> UpdateLocationMasterAsync(
        string id,
        UpdateLocationMasterRequest request,
        ILocationMasterRepository repository,
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

        var locationMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (locationMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(locationMaster);

        var updated = await repository.UpdateAsync(
            id,
            locationMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "LocationMaster",
            locationMaster.Id,
            locationMaster.LocationId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(LocationMasterResponse.FromEntity(locationMaster));
    }

    private static async Task<IResult> DeleteLocationMasterAsync(
        string id,
        ILocationMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid location id." });
        }

        var locationMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (locationMaster is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "LocationMaster",
            locationMaster.Id,
            locationMaster.LocationId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
