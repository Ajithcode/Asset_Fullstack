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

        group.MapPost("", CreateDataImportJobAsync).WithName("CreateDataImportJob");

        return group;
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
}
