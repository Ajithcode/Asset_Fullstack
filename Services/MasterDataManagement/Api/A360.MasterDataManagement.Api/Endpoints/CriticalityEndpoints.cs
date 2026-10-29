using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class CriticalityEndpoints
{
    public static RouteGroupBuilder MapCriticalityEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/criticalities")
            .WithTags("Criticalities");

        group.MapGet("", GetCriticalitiesAsync)
            .WithName("GetCriticalities");

        group.MapGet("/{id}", GetCriticalityByIdAsync)
            .WithName("GetCriticalityById");

        group.MapPost("", CreateCriticalityAsync)
            .WithName("CreateCriticality");

        group.MapPut("/{id}", UpdateCriticalityAsync)
            .WithName("UpdateCriticality");

        group.MapPatch("/{id}/status", UpdateCriticalityStatusAsync)
            .WithName("UpdateCriticalityStatus");

        group.MapDelete("/{id}", DeleteCriticalityAsync)
            .WithName("DeleteCriticality");

        return group;
    }

    private static async Task<IResult> GetCriticalitiesAsync(
        string? search,
        string? status,
        string? riskLevel,
        string? maintenancePriority,
        bool? defaultCriticality,
        string? clientId,
        ICriticalityRepository repository,
        CancellationToken cancellationToken)
    {
        var criticalities = await repository.SearchAsync(
            new CriticalityFilter(search, status, riskLevel, maintenancePriority, defaultCriticality, clientId),
            cancellationToken);

        return Results.Ok(
            criticalities.Select(CriticalityResponse.FromEntity));
    }

    private static async Task<IResult> GetCriticalityByIdAsync(
        string id,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> CreateCriticalityAsync(
        CreateCriticalityRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            criticality.Code,
            criticality.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A criticality with code '{criticality.Code}' already exists." });
        }

        criticality = await repository.CreateAsync(
            criticality,
            cancellationToken);

        if (criticality.DefaultCriticality)
        {
            await repository.ClearDefaultAsync(
                criticality.ClientId,
                criticality.Id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/criticalities/{criticality.Id}",
            CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> UpdateCriticalityAsync(
        string id,
        UpdateCriticalityRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(criticality);

        var existing = await repository.GetByCodeAsync(
            criticality.Code,
            criticality.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A criticality with code '{criticality.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            criticality,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (criticality.DefaultCriticality)
        {
            await repository.ClearDefaultAsync(
                criticality.ClientId,
                id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> UpdateCriticalityStatusAsync(
        string id,
        UpdateCriticalityStatusRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        criticality.Status = request.Status;
        criticality.UpdatedBy = request.UpdatedBy;
        criticality.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            criticality,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> DeleteCriticalityAsync(
        string id,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
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
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
