using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class FailureReasonEndpoints
{
    private const string SequenceName = "failure_reason";
    private const string FailureReasonCodePrefix = "FR";

    public static RouteGroupBuilder MapFailureReasonEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/failure-reasons").WithTags("FailureReasons");

        group.MapGet("", GetFailureReasonsAsync).WithName("GetFailureReasons");
        group.MapGet("/{id}", GetFailureReasonByIdAsync).WithName("GetFailureReasonById");
        group.MapPost("", CreateFailureReasonAsync).WithName("CreateFailureReason");
        group.MapPut("/{id}", UpdateFailureReasonAsync).WithName("UpdateFailureReason");
        group.MapDelete("/{id}", DeleteFailureReasonAsync).WithName("DeleteFailureReason");

        return group;
    }

    private static async Task<IResult> GetFailureReasonsAsync(
        IFailureReasonRepository repository,
        CancellationToken cancellationToken)
    {
        var failureReasons = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(failureReasons.Select(FailureReasonResponse.FromEntity));
    }

    private static async Task<IResult> GetFailureReasonByIdAsync(
        string id,
        IFailureReasonRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid failure reason id." });
        }

        var failureReason = await repository.GetByIdAsync(id, cancellationToken);
        if (failureReason is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("FailureReason", failureReason.FailureReasonCode, failureReason.FailureReasonName, EventAction.Viewed, cancellationToken);
        return Results.Ok(FailureReasonResponse.FromEntity(failureReason));
    }

    private static async Task<IResult> CreateFailureReasonAsync(
        CreateFailureReasonRequest request,
        IFailureReasonRepository repository,
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
        var failureReasonCode = $"{FailureReasonCodePrefix}{nextSequence:D6}";

        var failureReason = await repository.CreateAsync(request.ToEntity(failureReasonCode), cancellationToken);

        await eventLogger.LogAsync("FailureReason", failureReason.FailureReasonCode, failureReason.FailureReasonName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/failure-reasons/{failureReason.Id}", FailureReasonResponse.FromEntity(failureReason));
    }

    private static async Task<IResult> UpdateFailureReasonAsync(
        string id,
        UpdateFailureReasonRequest request,
        IFailureReasonRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid failure reason id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var failureReason = await repository.GetByIdAsync(id, cancellationToken);
        if (failureReason is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(failureReason);

        var updated = await repository.UpdateAsync(id, failureReason, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("FailureReason", failureReason.FailureReasonCode, failureReason.FailureReasonName, EventAction.Updated, cancellationToken);
        return Results.Ok(FailureReasonResponse.FromEntity(failureReason));
    }

    private static async Task<IResult> DeleteFailureReasonAsync(
        string id,
        IFailureReasonRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid failure reason id." });
        }

        var failureReason = await repository.GetByIdAsync(id, cancellationToken);
        if (failureReason is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("FailureReason", failureReason.FailureReasonCode, failureReason.FailureReasonName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
