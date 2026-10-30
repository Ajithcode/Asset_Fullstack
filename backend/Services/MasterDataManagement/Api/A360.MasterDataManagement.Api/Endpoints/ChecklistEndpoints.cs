using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ChecklistEndpoints
{
    public static RouteGroupBuilder MapChecklistEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/checklists")
            .WithTags("Checklists");

        group.MapGet("", GetChecklistsAsync)
            .WithName("GetChecklists");

        group.MapGet("/{id}", GetChecklistByIdAsync)
            .WithName("GetChecklistById");

        group.MapPost("", CreateChecklistAsync)
            .WithName("CreateChecklist");

        group.MapPut("/{id}", UpdateChecklistAsync)
            .WithName("UpdateChecklist");

        group.MapPatch("/{id}/status", UpdateChecklistStatusAsync)
            .WithName("UpdateChecklistStatus");

        group.MapDelete("/{id}", DeleteChecklistAsync)
            .WithName("DeleteChecklist");

        return group;
    }

    private static async Task<IResult> GetChecklistsAsync(
        string? search,
        string? status,
        string? checklistType,
        string? category,
        string? clientId,
        IChecklistRepository repository,
        CancellationToken cancellationToken)
    {
        var checklists = await repository.SearchAsync(
            new ChecklistFilter(search, status, checklistType, category, clientId),
            cancellationToken);

        return Results.Ok(
            checklists.Select(ChecklistResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistByIdAsync(
        string id,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var checklist = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklist is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Checklist",
            checklist.Id,
            checklist.ChecklistName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> CreateChecklistAsync(
        CreateChecklistRequest request,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklist = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            checklist.Code,
            checklist.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A checklist with code '{checklist.Code}' already exists." });
        }

        checklist = await repository.CreateAsync(
            checklist,
            cancellationToken);

        await eventLogger.LogAsync(
            "Checklist",
            checklist.Id,
            checklist.ChecklistName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/checklists/{checklist.Id}",
            ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> UpdateChecklistAsync(
        string id,
        UpdateChecklistRequest request,
        IChecklistRepository repository,
        IWorkTypeRepository workTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklist = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklist is null)
        {
            return Results.NotFound();
        }

        var previousCode = checklist.Code;

        request.ApplyTo(checklist);

        var existing = await repository.GetByCodeAsync(
            checklist.Code,
            checklist.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A checklist with code '{checklist.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            checklist,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        // Work types point at the checklist by code
        if (previousCode != checklist.Code)
        {
            await workTypes.UpdateChecklistCodeAsync(
                previousCode,
                checklist.Code,
                checklist.ClientId,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Checklist",
            checklist.Id,
            checklist.ChecklistName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ChecklistResponse.FromEntity(checklist));
    }

    private static async Task<IResult> UpdateChecklistStatusAsync(
        string id,
        UpdateChecklistStatusRequest request,
        IChecklistRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklist = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklist is null)
        {
            return Results.NotFound();
        }

        checklist.Status = request.Status;
        checklist.UpdatedBy = request.UpdatedBy;
        checklist.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            checklist,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Checklist",
            checklist.Id,
            checklist.ChecklistName,
            EventAction.Updated,
            cancellationToken);

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
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var checklist = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklist is null)
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
            "Checklist",
            checklist.Id,
            checklist.ChecklistName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
