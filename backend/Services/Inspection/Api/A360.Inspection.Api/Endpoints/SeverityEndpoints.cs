using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class SeverityEndpoints
{
    private const string SequenceName = "severity";
    private const string SeverityCodePrefix = "SEV";

    public static RouteGroupBuilder MapSeverityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/severities").WithTags("Severities");

        group.MapGet("", GetSeveritiesAsync).WithName("GetSeverities");
        group.MapGet("/{id}", GetSeverityByIdAsync).WithName("GetSeverityById");
        group.MapPost("", CreateSeverityAsync).WithName("CreateSeverity");
        group.MapPut("/{id}", UpdateSeverityAsync).WithName("UpdateSeverity");
        group.MapDelete("/{id}", DeleteSeverityAsync).WithName("DeleteSeverity");

        return group;
    }

    private static async Task<IResult> GetSeveritiesAsync(
        ISeverityRepository repository,
        CancellationToken cancellationToken)
    {
        var severities = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(severities.Select(SeverityResponse.FromEntity));
    }

    private static async Task<IResult> GetSeverityByIdAsync(
        string id,
        ISeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid severity id." });
        }

        var severity = await repository.GetByIdAsync(id, cancellationToken);
        if (severity is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Severity", severity.SeverityCode, severity.SeverityName, EventAction.Viewed, cancellationToken);
        return Results.Ok(SeverityResponse.FromEntity(severity));
    }

    private static async Task<IResult> CreateSeverityAsync(
        CreateSeverityRequest request,
        ISeverityRepository repository,
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
        var severityCode = $"{SeverityCodePrefix}{nextSequence:D6}";

        var severity = await repository.CreateAsync(request.ToEntity(severityCode), cancellationToken);

        await eventLogger.LogAsync("Severity", severity.SeverityCode, severity.SeverityName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/severities/{severity.Id}", SeverityResponse.FromEntity(severity));
    }

    private static async Task<IResult> UpdateSeverityAsync(
        string id,
        UpdateSeverityRequest request,
        ISeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid severity id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var severity = await repository.GetByIdAsync(id, cancellationToken);
        if (severity is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(severity);

        var updated = await repository.UpdateAsync(id, severity, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Severity", severity.SeverityCode, severity.SeverityName, EventAction.Updated, cancellationToken);
        return Results.Ok(SeverityResponse.FromEntity(severity));
    }

    private static async Task<IResult> DeleteSeverityAsync(
        string id,
        ISeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid severity id." });
        }

        var severity = await repository.GetByIdAsync(id, cancellationToken);
        if (severity is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Severity", severity.SeverityCode, severity.SeverityName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
