using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class ChecklistEndpoints
{
    private const string SequenceName = "checklist";
    private const string ChecklistCodePrefix = "CHK";

    public static RouteGroupBuilder MapChecklistEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/checklists").WithTags("Checklists");

        group.MapGet("", GetChecklistsAsync).WithName("GetChecklists");
        group.MapGet("/{id}", GetChecklistByIdAsync).WithName("GetChecklistById");
        group.MapPost("", CreateChecklistAsync).WithName("CreateChecklist");
        group.MapPut("/{id}", UpdateChecklistAsync).WithName("UpdateChecklist");
        group.MapDelete("/{id}", DeleteChecklistAsync).WithName("DeleteChecklist");

        return group;
    }

    private static async Task<IResult> GetChecklistsAsync(
        IChecklistRepository repository,
        CancellationToken cancellationToken)
    {
        var checklists = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(checklists.Select(ChecklistResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistByIdAsync(
        string id,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid checklist id." });
        }

        var checklist = await repository.GetByIdAsync(id, cancellationToken);
        if (checklist is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Checklist", checklist.ChecklistCode, checklist.TemplateName, EventAction.Viewed, cancellationToken);
        return Results.Ok(ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> CreateChecklistAsync(
        CreateChecklistRequest request,
        IChecklistRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var checklistCode = $"{ChecklistCodePrefix}{nextSequence:D6}";

        var checklist = await repository.CreateAsync(request.ToEntity(checklistCode), cancellationToken);

        await eventLogger.LogAsync("Checklist", checklist.ChecklistCode, checklist.TemplateName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/checklists/{checklist.Id}", ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> UpdateChecklistAsync(
        string id,
        UpdateChecklistRequest request,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid checklist id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklist = await repository.GetByIdAsync(id, cancellationToken);
        if (checklist is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(checklist);

        var updated = await repository.UpdateAsync(id, checklist, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Checklist", checklist.ChecklistCode, checklist.TemplateName, EventAction.Updated, cancellationToken);
        return Results.Ok(ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> DeleteChecklistAsync(
        string id,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid checklist id." });
        }

        var checklist = await repository.GetByIdAsync(id, cancellationToken);
        if (checklist is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Checklist", checklist.ChecklistCode, checklist.TemplateName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
