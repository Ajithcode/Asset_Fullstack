using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class ChecklistItemEndpoints
{
    private const string EntityType = "ChecklistItem";

    public static RouteGroupBuilder MapChecklistItemEndpoints(this IEndpointRouteBuilder routes)
    {
        // Prefixed with /administration because the WIP service already has its own checklist items.
        var group = routes.MapGroup("/api/administration/checklist-items").WithTags("Checklist Items");

        group.MapGet("", GetChecklistItemsAsync).WithName("GetChecklistItems");
        group.MapGet("/{itemId}", GetChecklistItemByIdAsync).WithName("GetChecklistItemById");
        group.MapPost("", CreateChecklistItemAsync).WithName("CreateChecklistItem");
        group.MapPut("/{itemId}", UpdateChecklistItemAsync).WithName("UpdateChecklistItem");
        group.MapDelete("/{itemId}", DeleteChecklistItemAsync).WithName("DeleteChecklistItem");

        return group;
    }

    private static async Task<IResult> GetChecklistItemsAsync(
        string? tenantId,
        string? clientId,
        string? section,
        IChecklistItemRepository repository,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetAsync(tenantId, clientId, section, cancellationToken);

        return Results.Ok(items.Select(ChecklistItemResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistItemByIdAsync(
        string itemId,
        IChecklistItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetByItemIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return NotFound(itemId);
        }

        await eventLogger.LogAsync(EntityType, item.Id, item.ItemText, EventAction.Viewed, cancellationToken);

        return Results.Ok(ChecklistItemResponse.FromEntity(item));
    }

    private static async Task<IResult> CreateChecklistItemAsync(
        CreateChecklistItemRequest request,
        IChecklistItemRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var item = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, item.Id, item.ItemText, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/checklist-items/{item.ItemId}",
            ChecklistItemResponse.FromEntity(item));
    }

    private static async Task<IResult> UpdateChecklistItemAsync(
        string itemId,
        UpdateChecklistItemRequest request,
        IChecklistItemRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var item = await repository.GetByItemIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return NotFound(itemId);
        }

        request.ApplyTo(item, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(item.Id, item, cancellationToken))
        {
            return NotFound(itemId);
        }

        await eventLogger.LogAsync(EntityType, item.Id, item.ItemText, EventAction.Updated, cancellationToken);

        return Results.Ok(ChecklistItemResponse.FromEntity(item));
    }

    // Soft delete: the document is kept and flagged IsDeleted so the history stays auditable.
    private static async Task<IResult> DeleteChecklistItemAsync(
        string itemId,
        IChecklistItemRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetByItemIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return NotFound(itemId);
        }

        item.MarkDeleted(currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(item.Id, item, cancellationToken))
        {
            return NotFound(itemId);
        }

        await eventLogger.LogAsync(EntityType, item.Id, item.ItemText, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string itemId)
    {
        return Results.NotFound(new { message = $"Checklist item '{itemId}' was not found." });
    }
}
