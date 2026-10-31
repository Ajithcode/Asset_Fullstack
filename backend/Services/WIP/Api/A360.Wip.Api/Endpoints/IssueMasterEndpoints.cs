
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class IssueMasterEndpoints
{
    public static RouteGroupBuilder MapIssueMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/issue-masters")
            .WithTags("IssueMasters");

        group.MapGet("", GetIssueMastersAsync)
            .WithName("GetIssueMasters");

        group.MapGet("/{id}", GetIssueMasterByIdAsync)
            .WithName("GetIssueMasterById");

        group.MapGet("/asset/{assetId}", GetIssueMastersByAssetIdAsync)
            .WithName("GetIssueMastersByAssetId");

        group.MapGet("/job/{jobId}", GetIssueMastersByJobIdAsync)
            .WithName("GetIssueMastersByJobId");

        group.MapPost("", CreateIssueMasterAsync)
            .WithName("CreateIssueMaster");

        group.MapPut("/{id}", UpdateIssueMasterAsync)
            .WithName("UpdateIssueMaster");

        group.MapDelete("/{id}", DeleteIssueMasterAsync)
            .WithName("DeleteIssueMaster");

        return group;
    }

    private static async Task<IResult> GetIssueMastersAsync(
        IIssueMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var issueMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            issueMasters.Select(IssueMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetIssueMasterByIdAsync(
        string id,
        IIssueMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issue id." });
        }

        var issueMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "IssueMaster",
            issueMaster.Id,
            issueMaster.IssueId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(IssueMasterResponse.FromEntity(issueMaster));
    }

    private static async Task<IResult> GetIssueMastersByAssetIdAsync(
        string assetId,
        IIssueMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var issueMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            issueMasters.Select(IssueMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetIssueMastersByJobIdAsync(
        string jobId,
        IIssueMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Results.BadRequest(
                new { message = "Job id is required." });
        }

        var issueMasters = await repository.GetByJobIdAsync(
            jobId,
            cancellationToken);

        return Results.Ok(
            issueMasters.Select(IssueMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateIssueMasterAsync(
        CreateIssueMasterRequest request,
        IIssueMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var issueMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "IssueMaster",
            issueMaster.Id,
            issueMaster.IssueId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/issue-masters/{issueMaster.Id}",
            IssueMasterResponse.FromEntity(issueMaster));
    }

    private static async Task<IResult> UpdateIssueMasterAsync(
        string id,
        UpdateIssueMasterRequest request,
        IIssueMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issue id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var issueMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(issueMaster);

        var updated = await repository.UpdateAsync(
            id,
            issueMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "IssueMaster",
            issueMaster.Id,
            issueMaster.IssueId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(IssueMasterResponse.FromEntity(issueMaster));
    }

    private static async Task<IResult> DeleteIssueMasterAsync(
        string id,
        IIssueMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issue id." });
        }

        var issueMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueMaster is null)
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
            "IssueMaster",
            issueMaster.Id,
            issueMaster.IssueId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
