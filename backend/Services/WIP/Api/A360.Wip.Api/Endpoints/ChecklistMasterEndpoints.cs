
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class ChecklistMasterEndpoints
{
    public static RouteGroupBuilder MapChecklistMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/checklist-masters")
            .WithTags("ChecklistMasters");

        group.MapGet("", GetChecklistMastersAsync)
            .WithName("GetChecklistMasters");

        group.MapGet("/{id}", GetChecklistMasterByIdAsync)
            .WithName("GetChecklistMasterById");

        group.MapGet("/asset/{assetId}", GetChecklistMastersByAssetIdAsync)
            .WithName("GetChecklistMastersByAssetId");

        group.MapPost("", CreateChecklistMasterAsync)
            .WithName("CreateChecklistMaster");

        group.MapPut("/{id}", UpdateChecklistMasterAsync)
            .WithName("UpdateChecklistMaster");

        group.MapDelete("/{id}", DeleteChecklistMasterAsync)
            .WithName("DeleteChecklistMaster");

        return group;
    }

    private static async Task<IResult> GetChecklistMastersAsync(
        IChecklistMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var checklistMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            checklistMasters.Select(ChecklistMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetChecklistMasterByIdAsync(
        string id,
        IChecklistMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var checklistMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChecklistMaster",
            checklistMaster.Id,
            checklistMaster.ChecklistId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ChecklistMasterResponse.FromEntity(checklistMaster));
    }

    private static async Task<IResult> GetChecklistMastersByAssetIdAsync(
        string assetId,
        IChecklistMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var checklistMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            checklistMasters.Select(ChecklistMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateChecklistMasterAsync(
        CreateChecklistMasterRequest request,
        IChecklistMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var checklistMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "ChecklistMaster",
            checklistMaster.Id,
            checklistMaster.ChecklistId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/checklist-masters/{checklistMaster.Id}",
            ChecklistMasterResponse.FromEntity(checklistMaster));
    }

    private static async Task<IResult> UpdateChecklistMasterAsync(
        string id,
        UpdateChecklistMasterRequest request,
        IChecklistMasterRepository repository,
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

        var checklistMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(checklistMaster);

        var updated = await repository.UpdateAsync(
            id,
            checklistMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChecklistMaster",
            checklistMaster.Id,
            checklistMaster.ChecklistId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ChecklistMasterResponse.FromEntity(checklistMaster));
    }

    private static async Task<IResult> DeleteChecklistMasterAsync(
        string id,
        IChecklistMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid checklist id." });
        }

        var checklistMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (checklistMaster is null)
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
            "ChecklistMaster",
            checklistMaster.Id,
            checklistMaster.ChecklistId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
