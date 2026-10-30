
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class SparePartEndpoints
{
    public static RouteGroupBuilder MapSparePartEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/spare-parts")
            .WithTags("SpareParts");

        group.MapGet("", GetSparePartsAsync)
            .WithName("GetSpareParts");

        group.MapGet("/{id}", GetSparePartByIdAsync)
            .WithName("GetSparePartById");

        group.MapGet("/asset/{assetId}", GetSparePartsByAssetIdAsync)
            .WithName("GetSparePartsByAssetId");

        group.MapPost("", CreateSparePartAsync)
            .WithName("CreateSparePart");

        group.MapPut("/{id}", UpdateSparePartAsync)
            .WithName("UpdateSparePart");

        group.MapDelete("/{id}", DeleteSparePartAsync)
            .WithName("DeleteSparePart");

        return group;
    }

    private static async Task<IResult> GetSparePartsAsync(
        ISparePartRepository repository,
        CancellationToken cancellationToken)
    {
        var spareParts = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            spareParts.Select(SparePartResponse.FromEntity));
    }

    private static async Task<IResult> GetSparePartByIdAsync(
        string id,
        ISparePartRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid spareparts id." });
        }

        var sparePart = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (sparePart is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SparePart",
            sparePart.Id,
            sparePart.PartName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SparePartResponse.FromEntity(sparePart));
    }

    private static async Task<IResult> GetSparePartsByAssetIdAsync(
        string assetId,
        ISparePartRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var spareParts = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            spareParts.Select(SparePartResponse.FromEntity));
    }

    private static async Task<IResult> CreateSparePartAsync(
        CreateSparePartRequest request,
        ISparePartRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sparePart = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "SparePart",
            sparePart.Id,
            sparePart.PartName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/spare-parts/{sparePart.Id}",
            SparePartResponse.FromEntity(sparePart));
    }

    private static async Task<IResult> UpdateSparePartAsync(
        string id,
        UpdateSparePartRequest request,
        ISparePartRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid spareparts id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sparePart = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (sparePart is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(sparePart);

        var updated = await repository.UpdateAsync(
            id,
            sparePart,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SparePart",
            sparePart.Id,
            sparePart.PartName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SparePartResponse.FromEntity(sparePart));
    }

    private static async Task<IResult> DeleteSparePartAsync(
        string id,
        ISparePartRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid spareparts id." });
        }

        var sparePart = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (sparePart is null)
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
            "SparePart",
            sparePart.Id,
            sparePart.PartName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
