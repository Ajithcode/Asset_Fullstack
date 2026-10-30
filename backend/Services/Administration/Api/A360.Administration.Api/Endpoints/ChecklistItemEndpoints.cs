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

        group.MapPost("", CreateChecklistItemAsync).WithName("CreateChecklistItem");

        return group;
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
}
