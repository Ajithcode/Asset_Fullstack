
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class JobMasterEndpoints
{
    public static RouteGroupBuilder MapJobMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/job-masters")
            .WithTags("JobMasters");

        group.MapGet("", GetJobMastersAsync)
            .WithName("GetJobMasters");

        group.MapGet("/{id}", GetJobMasterByIdAsync)
            .WithName("GetJobMasterById");

        group.MapGet("/asset/{assetId}", GetJobMastersByAssetIdAsync)
            .WithName("GetJobMastersByAssetId");

        group.MapPost("", CreateJobMasterAsync)
            .WithName("CreateJobMaster");

        group.MapPut("/{id}", UpdateJobMasterAsync)
            .WithName("UpdateJobMaster");

        group.MapDelete("/{id}", DeleteJobMasterAsync)
            .WithName("DeleteJobMaster");

        return group;
    }

    private static async Task<IResult> GetJobMastersAsync(
        IJobMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var jobMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            jobMasters.Select(JobMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetJobMasterByIdAsync(
        string id,
        IJobMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid job id." });
        }

        var jobMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (jobMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "JobMaster",
            jobMaster.Id,
            jobMaster.JobId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(JobMasterResponse.FromEntity(jobMaster));
    }

    private static async Task<IResult> GetJobMastersByAssetIdAsync(
        string assetId,
        IJobMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var jobMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            jobMasters.Select(JobMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateJobMasterAsync(
        CreateJobMasterRequest request,
        IJobMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var jobMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "JobMaster",
            jobMaster.Id,
            jobMaster.JobId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/job-masters/{jobMaster.Id}",
            JobMasterResponse.FromEntity(jobMaster));
    }

    private static async Task<IResult> UpdateJobMasterAsync(
        string id,
        UpdateJobMasterRequest request,
        IJobMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid job id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var jobMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (jobMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(jobMaster);

        var updated = await repository.UpdateAsync(
            id,
            jobMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "JobMaster",
            jobMaster.Id,
            jobMaster.JobId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(JobMasterResponse.FromEntity(jobMaster));
    }

    private static async Task<IResult> DeleteJobMasterAsync(
        string id,
        IJobMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid job id." });
        }

        var jobMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (jobMaster is null)
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
            "JobMaster",
            jobMaster.Id,
            jobMaster.JobId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
