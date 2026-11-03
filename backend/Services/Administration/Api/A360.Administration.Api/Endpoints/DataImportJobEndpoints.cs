using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class DataImportJobEndpoints
{
    private const string EntityType = "DataImportJob";

    public static RouteGroupBuilder MapDataImportJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/administration/data-imports").WithTags("Data Imports");

        group.MapGet("", GetDataImportJobsAsync).WithName("GetDataImportJobs");
        group.MapGet("/{jobId}", GetDataImportJobByIdAsync).WithName("GetDataImportJobById");
        group.MapPost("", CreateDataImportJobAsync).WithName("CreateDataImportJob");
        group.MapPut("/{jobId}", UpdateDataImportJobAsync).WithName("UpdateDataImportJob");
        group.MapDelete("/{jobId}", DeleteDataImportJobAsync).WithName("DeleteDataImportJob");

        return group;
    }

    private static async Task<IResult> GetDataImportJobsAsync(
        string? tenantId,
        string? clientId,
        string? dataObject,
        string? status,
        IDataImportJobRepository repository,
        CancellationToken cancellationToken)
    {
        var jobs = await repository.GetAsync(tenantId, clientId, dataObject, status, cancellationToken);

        return Results.Ok(jobs.Select(DataImportJobResponse.FromEntity));
    }

    private static async Task<IResult> GetDataImportJobByIdAsync(
        string jobId,
        IDataImportJobRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByJobIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound(jobId);
        }

        await eventLogger.LogAsync(EntityType, job.Id, job.JobName, EventAction.Viewed, cancellationToken);

        return Results.Ok(DataImportJobResponse.FromEntity(job));
    }

    private static async Task<IResult> CreateDataImportJobAsync(
        CreateDataImportJobRequest request,
        IDataImportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var job = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, job.Id, job.JobName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/administration/data-imports/{job.JobId}", DataImportJobResponse.FromEntity(job));
    }

    private static async Task<IResult> UpdateDataImportJobAsync(
        string jobId,
        UpdateDataImportJobRequest request,
        IDataImportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var job = await repository.GetByJobIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound(jobId);
        }

        request.ApplyTo(job, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(job.Id, job, cancellationToken))
        {
            return NotFound(jobId);
        }

        await eventLogger.LogAsync(EntityType, job.Id, job.JobName, EventAction.Updated, cancellationToken);

        return Results.Ok(DataImportJobResponse.FromEntity(job));
    }

    private static async Task<IResult> DeleteDataImportJobAsync(
        string jobId,
        IDataImportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByJobIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound(jobId);
        }

        job.MarkDeleted(currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(job.Id, job, cancellationToken))
        {
            return NotFound(jobId);
        }

        await eventLogger.LogAsync(EntityType, job.Id, job.JobName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string jobId)
    {
        return Results.NotFound(new { message = $"Data import job '{jobId}' was not found" });
    }
}
