using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class PrioritySeverityEndpoints
{
    public static RouteGroupBuilder MapPrioritySeverityEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/priority-severities")
            .WithTags("PrioritySeverities");

        group.MapGet("", GetPrioritySeveritiesAsync)
            .WithName("GetPrioritySeverities");

        group.MapGet("/{id}", GetPrioritySeverityByIdAsync)
            .WithName("GetPrioritySeverityById");

        group.MapPost("", CreatePrioritySeverityAsync)
            .WithName("CreatePrioritySeverity");

        group.MapPut("/{id}", UpdatePrioritySeverityAsync)
            .WithName("UpdatePrioritySeverity");

        group.MapPatch("/{id}/status", UpdatePrioritySeverityStatusAsync)
            .WithName("UpdatePrioritySeverityStatus");

        group.MapDelete("/{id}", DeletePrioritySeverityAsync)
            .WithName("DeletePrioritySeverity");

        return group;
    }

    private static async Task<IResult> GetPrioritySeveritiesAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        IPrioritySeverityRepository repository,
        CancellationToken cancellationToken)
    {
        var prioritySeverities = await repository.SearchAsync(
            new PrioritySeverityFilter(search, status, type, clientId),
            cancellationToken);

        return Results.Ok(
            prioritySeverities.Select(PrioritySeverityResponse.FromEntity));
    }

    private static async Task<IResult> GetPrioritySeverityByIdAsync(
        string id,
        IPrioritySeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid priority/severity id." });
        }

        var prioritySeverity = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (prioritySeverity is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PrioritySeverity",
            prioritySeverity.Id,
            prioritySeverity.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PrioritySeverityResponse.FromEntity(prioritySeverity));
    }

    private static async Task<IResult> CreatePrioritySeverityAsync(
        CreatePrioritySeverityRequest request,
        IPrioritySeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var prioritySeverity = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            prioritySeverity.Code,
            prioritySeverity.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A priority/severity with code '{prioritySeverity.Code}' already exists." });
        }

        prioritySeverity = await repository.CreateAsync(
            prioritySeverity,
            cancellationToken);

        await eventLogger.LogAsync(
            "PrioritySeverity",
            prioritySeverity.Id,
            prioritySeverity.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/priority-severities/{prioritySeverity.Id}",
            PrioritySeverityResponse.FromEntity(prioritySeverity));
    }

    private static async Task<IResult> UpdatePrioritySeverityAsync(
        string id,
        UpdatePrioritySeverityRequest request,
        IPrioritySeverityRepository repository,
        IWorkTypeRepository workTypes,
        IMaintenanceTypeRepository maintenanceTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid priority/severity id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var prioritySeverity = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (prioritySeverity is null)
        {
            return Results.NotFound();
        }

        var previousName = prioritySeverity.Name;
        var previousType = prioritySeverity.Type;

        request.ApplyTo(prioritySeverity);

        var existing = await repository.GetByCodeAsync(
            prioritySeverity.Code,
            prioritySeverity.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A priority/severity with code '{prioritySeverity.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            prioritySeverity,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        // Work types and maintenance types keep their default priority by name
        if (previousType == "Priority" && previousName != prioritySeverity.Name)
        {
            await workTypes.UpdateDefaultPriorityAsync(
                previousName,
                prioritySeverity.Name,
                prioritySeverity.ClientId,
                cancellationToken);

            await maintenanceTypes.UpdateDefaultPriorityAsync(
                previousName,
                prioritySeverity.Name,
                prioritySeverity.ClientId,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "PrioritySeverity",
            prioritySeverity.Id,
            prioritySeverity.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PrioritySeverityResponse.FromEntity(prioritySeverity));
    }

    private static async Task<IResult> UpdatePrioritySeverityStatusAsync(
        string id,
        UpdatePrioritySeverityStatusRequest request,
        IPrioritySeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid priority/severity id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var prioritySeverity = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (prioritySeverity is null)
        {
            return Results.NotFound();
        }

        prioritySeverity.Status = request.Status;
        prioritySeverity.UpdatedBy = request.UpdatedBy;
        prioritySeverity.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            prioritySeverity,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PrioritySeverity",
            prioritySeverity.Id,
            prioritySeverity.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PrioritySeverityResponse.FromEntity(prioritySeverity));
    }

    private static async Task<IResult> DeletePrioritySeverityAsync(
        string id,
        IPrioritySeverityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid priority/severity id." });
        }

        var prioritySeverity = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (prioritySeverity is null)
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
            "PrioritySeverity",
            prioritySeverity.Id,
            prioritySeverity.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
