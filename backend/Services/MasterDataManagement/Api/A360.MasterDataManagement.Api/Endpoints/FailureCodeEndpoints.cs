using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class FailureCodeEndpoints
{
    public static RouteGroupBuilder MapFailureCodeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/failure-codes")
            .WithTags("FailureCodes");

        group.MapGet("", GetFailureCodesAsync)
            .WithName("GetFailureCodes");

        group.MapGet("/{id}", GetFailureCodeByIdAsync)
            .WithName("GetFailureCodeById");

        group.MapPost("", CreateFailureCodeAsync)
            .WithName("CreateFailureCode");

        group.MapPut("/{id}", UpdateFailureCodeAsync)
            .WithName("UpdateFailureCode");

        group.MapPatch("/{id}/status", UpdateFailureCodeStatusAsync)
            .WithName("UpdateFailureCodeStatus");

        group.MapDelete("/{id}", DeleteFailureCodeAsync)
            .WithName("DeleteFailureCode");

        return group;
    }

    private static async Task<IResult> GetFailureCodesAsync(
        string? search,
        string? status,
        string? category,
        string? severity,
        string? clientId,
        IFailureCodeRepository repository,
        CancellationToken cancellationToken)
    {
        var failureCodes = await repository.SearchAsync(
            new FailureCodeFilter(search, status, category, severity, clientId),
            cancellationToken);

        return Results.Ok(
            failureCodes.Select(FailureCodeResponse.FromEntity));
    }

    private static async Task<IResult> GetFailureCodeByIdAsync(
        string id,
        IFailureCodeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid failure code id." });
        }

        var failureCode = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (failureCode is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "FailureCode",
            failureCode.Id,
            failureCode.FailureCodeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(FailureCodeResponse.FromEntity(failureCode));
    }

    private static async Task<IResult> CreateFailureCodeAsync(
        CreateFailureCodeRequest request,
        IFailureCodeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var failureCode = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            failureCode.Code,
            failureCode.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A failure code with code '{failureCode.Code}' already exists." });
        }

        failureCode = await repository.CreateAsync(
            failureCode,
            cancellationToken);

        await eventLogger.LogAsync(
            "FailureCode",
            failureCode.Id,
            failureCode.FailureCodeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/failure-codes/{failureCode.Id}",
            FailureCodeResponse.FromEntity(failureCode));
    }

    private static async Task<IResult> UpdateFailureCodeAsync(
        string id,
        UpdateFailureCodeRequest request,
        IFailureCodeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid failure code id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var failureCode = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (failureCode is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(failureCode);

        var existing = await repository.GetByCodeAsync(
            failureCode.Code,
            failureCode.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A failure code with code '{failureCode.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            failureCode,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "FailureCode",
            failureCode.Id,
            failureCode.FailureCodeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(FailureCodeResponse.FromEntity(failureCode));
    }

    private static async Task<IResult> UpdateFailureCodeStatusAsync(
        string id,
        UpdateFailureCodeStatusRequest request,
        IFailureCodeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid failure code id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var failureCode = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (failureCode is null)
        {
            return Results.NotFound();
        }

        failureCode.Status = request.Status;
        failureCode.UpdatedBy = request.UpdatedBy;
        failureCode.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            failureCode,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "FailureCode",
            failureCode.Id,
            failureCode.FailureCodeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(FailureCodeResponse.FromEntity(failureCode));
    }

    private static async Task<IResult> DeleteFailureCodeAsync(
        string id,
        IFailureCodeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid failure code id." });
        }

        var failureCode = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (failureCode is null)
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
            "FailureCode",
            failureCode.Id,
            failureCode.FailureCodeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
