using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ConditionEndpoints
{
    public static RouteGroupBuilder MapConditionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/conditions")
            .WithTags("Conditions");

        group.MapGet("", GetConditionsAsync)
            .WithName("GetConditions");

        group.MapGet("/{id}", GetConditionByIdAsync)
            .WithName("GetConditionById");

        group.MapPost("", CreateConditionAsync)
            .WithName("CreateCondition");

        group.MapPut("/{id}", UpdateConditionAsync)
            .WithName("UpdateCondition");

        group.MapPatch("/{id}/status", UpdateConditionStatusAsync)
            .WithName("UpdateConditionStatus");

        group.MapDelete("/{id}", DeleteConditionAsync)
            .WithName("DeleteCondition");

        return group;
    }

    private static async Task<IResult> GetConditionsAsync(
        string? search,
        string? status,
        string? type,
        bool? defaultCondition,
        string? clientId,
        IConditionRepository repository,
        CancellationToken cancellationToken)
    {
        var conditions = await repository.SearchAsync(
            new ConditionFilter(search, status, type, defaultCondition, clientId),
            cancellationToken);

        return Results.Ok(
            conditions.Select(ConditionResponse.FromEntity));
    }

    private static async Task<IResult> GetConditionByIdAsync(
        string id,
        IConditionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid condition id." });
        }

        var condition = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (condition is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Condition",
            condition.Id,
            condition.ConditionName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ConditionResponse.FromEntity(condition));
    }

    private static async Task<IResult> CreateConditionAsync(
        CreateConditionRequest request,
        IConditionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var condition = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            condition.Code,
            condition.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A condition with code '{condition.Code}' already exists." });
        }

        condition = await repository.CreateAsync(
            condition,
            cancellationToken);

        if (condition.DefaultCondition)
        {
            await repository.ClearDefaultAsync(
                condition.ClientId,
                condition.Id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Condition",
            condition.Id,
            condition.ConditionName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/conditions/{condition.Id}",
            ConditionResponse.FromEntity(condition));
    }

    private static async Task<IResult> UpdateConditionAsync(
        string id,
        UpdateConditionRequest request,
        IConditionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid condition id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var condition = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (condition is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(condition);

        var existing = await repository.GetByCodeAsync(
            condition.Code,
            condition.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A condition with code '{condition.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            condition,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (condition.DefaultCondition)
        {
            await repository.ClearDefaultAsync(
                condition.ClientId,
                id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Condition",
            condition.Id,
            condition.ConditionName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ConditionResponse.FromEntity(condition));
    }

    private static async Task<IResult> UpdateConditionStatusAsync(
        string id,
        UpdateConditionStatusRequest request,
        IConditionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid condition id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var condition = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (condition is null)
        {
            return Results.NotFound();
        }

        condition.Status = request.Status;
        condition.UpdatedBy = request.UpdatedBy;
        condition.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            condition,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Condition",
            condition.Id,
            condition.ConditionName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ConditionResponse.FromEntity(condition));
    }

    private static async Task<IResult> DeleteConditionAsync(
        string id,
        IConditionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid condition id." });
        }

        var condition = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (condition is null)
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
            "Condition",
            condition.Id,
            condition.ConditionName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
