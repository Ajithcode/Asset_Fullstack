
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class ChecklistItemEndpoints
{
    public static RouteGroupBuilder MapChecklistItemEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/checklist-items")
            .WithTags("ChecklistItems");

        group.MapGet("", GetChecklistItemsAsync)
            .WithName("GetChecklistItems");

        group.MapGet("/{id}", GetChecklistItemByIdAsync)
            .WithName("GetChecklistItemById");

        group.MapGet("/asset/{assetId}", GetChecklistItemsByAssetIdAsync)
            .WithName("GetChecklistItemsByAssetId");

        group.MapGet("/checklist/{checklistId}", GetChecklistItemsByChecklistIdAsync)
            .WithName("GetChecklistItemsByChecklistId");

        group.MapPost("", CreateChecklistItemAsync)
            .WithName("CreateChecklistItem");

        group.MapPut("/{id}", UpdateChecklistItemAsync)
            .WithName("UpdateChecklistItem");

        group.MapDelete("/{id}", DeleteChecklistItemAsync)
            .WithName("DeleteChecklistItem");

        return group;
    }

    private static async Task<IResult> GetChecklistItemsAsync(
        IChecklistItemRepository repository,
        CancellationToken cancellationToken)
    {
        var checklistItems = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            checklistItems.Select(ChecklistItemResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistItemByIdAsync(
        string id,
        IChecklistItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var checklistItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistItem is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChecklistItem",
            checklistItem.Id,
            checklistItem.ItemId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ChecklistItemResponse.FromEntity(checklistItem));
    }

    private static async Task<IResult> GetChecklistItemsByAssetIdAsync(
        string assetId,
        IChecklistItemRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var checklistItems = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            checklistItems.Select(ChecklistItemResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistItemsByChecklistIdAsync(
        string checklistId,
        IChecklistItemRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(checklistId))
        {
            return Results.BadRequest(
                new { message = "Checklist id is required." });
        }

        var checklistItems = await repository.GetByChecklistIdAsync(
            checklistId,
            cancellationToken);

        return Results.Ok(
            checklistItems.Select(ChecklistItemResponse.FromEntity));
    }

    private static async Task<IResult> CreateChecklistItemAsync(
        CreateChecklistItemRequest request,
        IChecklistItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklistItem = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "ChecklistItem",
            checklistItem.Id,
            checklistItem.ItemId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/checklist-items/{checklistItem.Id}",
            ChecklistItemResponse.FromEntity(checklistItem));
    }

    private static async Task<IResult> UpdateChecklistItemAsync(
        string id,
        UpdateChecklistItemRequest request,
        IChecklistItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklistItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistItem is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(checklistItem);

        var updated = await repository.UpdateAsync(
            id,
            checklistItem,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChecklistItem",
            checklistItem.Id,
            checklistItem.ItemId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ChecklistItemResponse.FromEntity(checklistItem));
    }

    private static async Task<IResult> DeleteChecklistItemAsync(
        string id,
        IChecklistItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var checklistItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistItem is null)
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
            "ChecklistItem",
            checklistItem.Id,
            checklistItem.ItemId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
