
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class ProgressLogEndpoints
{
    public static RouteGroupBuilder MapProgressLogEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/progress-logs")
            .WithTags("ProgressLogs");

        group.MapGet("", GetProgressLogsAsync)
            .WithName("GetProgressLogs");

        group.MapGet("/{id}", GetProgressLogByIdAsync)
            .WithName("GetProgressLogById");

        group.MapGet("/asset/{assetId}", GetProgressLogsByAssetIdAsync)
            .WithName("GetProgressLogsByAssetId");

        group.MapGet("/job/{jobId}", GetProgressLogsByJobIdAsync)
            .WithName("GetProgressLogsByJobId");

        group.MapGet("/task/{taskId}", GetProgressLogsByTaskIdAsync)
            .WithName("GetProgressLogsByTaskId");

        group.MapPost("", CreateProgressLogAsync)
            .WithName("CreateProgressLog");

        group.MapPut("/{id}", UpdateProgressLogAsync)
            .WithName("UpdateProgressLog");

        group.MapDelete("/{id}", DeleteProgressLogAsync)
            .WithName("DeleteProgressLog");

        return group;
    }

    private static async Task<IResult> GetProgressLogsAsync(
        IProgressLogRepository repository,
        CancellationToken cancellationToken)
    {
        var progressLogs = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            progressLogs.Select(ProgressLogResponse.FromEntity));
    }

    private static async Task<IResult> GetProgressLogByIdAsync(
        string id,
        IProgressLogRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid log id." });
        }

        var progressLog = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (progressLog is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ProgressLog",
            progressLog.Id,
            progressLog.LogId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ProgressLogResponse.FromEntity(progressLog));
    }

    private static async Task<IResult> GetProgressLogsByAssetIdAsync(
        string assetId,
        IProgressLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var progressLogs = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            progressLogs.Select(ProgressLogResponse.FromEntity));
    }

    private static async Task<IResult> GetProgressLogsByJobIdAsync(
        string jobId,
        IProgressLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Results.BadRequest(
                new { message = "Job id is required." });
        }

        var progressLogs = await repository.GetByJobIdAsync(
            jobId,
            cancellationToken);

        return Results.Ok(
            progressLogs.Select(ProgressLogResponse.FromEntity));
    }

    private static async Task<IResult> GetProgressLogsByTaskIdAsync(
        string taskId,
        IProgressLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Results.BadRequest(
                new { message = "Task id is required." });
        }

        var progressLogs = await repository.GetByTaskIdAsync(
            taskId,
            cancellationToken);

        return Results.Ok(
            progressLogs.Select(ProgressLogResponse.FromEntity));
    }

    private static async Task<IResult> CreateProgressLogAsync(
        CreateProgressLogRequest request,
        IProgressLogRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var progressLog = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "ProgressLog",
            progressLog.Id,
            progressLog.LogId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/progress-logs/{progressLog.Id}",
            ProgressLogResponse.FromEntity(progressLog));
    }

    private static async Task<IResult> UpdateProgressLogAsync(
        string id,
        UpdateProgressLogRequest request,
        IProgressLogRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid log id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var progressLog = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (progressLog is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(progressLog);

        var updated = await repository.UpdateAsync(
            id,
            progressLog,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ProgressLog",
            progressLog.Id,
            progressLog.LogId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ProgressLogResponse.FromEntity(progressLog));
    }

    private static async Task<IResult> DeleteProgressLogAsync(
        string id,
        IProgressLogRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid log id." });
        }

        var progressLog = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (progressLog is null)
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
            "ProgressLog",
            progressLog.Id,
            progressLog.LogId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
