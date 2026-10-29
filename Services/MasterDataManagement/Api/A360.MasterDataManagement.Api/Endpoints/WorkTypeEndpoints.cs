using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class WorkTypeEndpoints
{
    public static RouteGroupBuilder MapWorkTypeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/work-types")
            .WithTags("WorkTypes");

        group.MapGet("", GetWorkTypesAsync)
            .WithName("GetWorkTypes");

        group.MapGet("/{id}", GetWorkTypeByIdAsync)
            .WithName("GetWorkTypeById");

        group.MapPost("", CreateWorkTypeAsync)
            .WithName("CreateWorkType");

        group.MapPut("/{id}", UpdateWorkTypeAsync)
            .WithName("UpdateWorkType");

        group.MapPatch("/{id}/status", UpdateWorkTypeStatusAsync)
            .WithName("UpdateWorkTypeStatus");

        group.MapDelete("/{id}", DeleteWorkTypeAsync)
            .WithName("DeleteWorkType");

        return group;
    }

    private static async Task<IResult> GetWorkTypesAsync(
        string? search,
        string? status,
        string? maintenanceTypeId,
        string? clientId,
        IWorkTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var workTypes = await repository.SearchAsync(
            new WorkTypeFilter(search, status, maintenanceTypeId, clientId),
            cancellationToken);

        return Results.Ok(
            workTypes.Select(WorkTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkTypeByIdAsync(
        string id,
        IWorkTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work type id." });
        }

        var workType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkType",
            workType.Id,
            workType.WorkTypeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkTypeResponse.FromEntity(workType));
    }

    private static async Task<IResult> CreateWorkTypeAsync(
        CreateWorkTypeRequest request,
        IWorkTypeRepository repository,
        IMaintenanceTypeRepository maintenanceTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workType = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            workType.Code,
            workType.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A work type with code '{workType.Code}' already exists." });
        }

        var maintenanceTypeError = await MasterDataReferenceResolver.ResolveMaintenanceTypeAsync(
            workType.MaintenanceType,
            maintenanceTypes,
            cancellationToken);

        if (maintenanceTypeError is not null)
        {
            return maintenanceTypeError;
        }

        workType = await repository.CreateAsync(
            workType,
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkType",
            workType.Id,
            workType.WorkTypeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/work-types/{workType.Id}",
            WorkTypeResponse.FromEntity(workType));
    }

    private static async Task<IResult> UpdateWorkTypeAsync(
        string id,
        UpdateWorkTypeRequest request,
        IWorkTypeRepository repository,
        IMaintenanceTypeRepository maintenanceTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workType);

        var existing = await repository.GetByCodeAsync(
            workType.Code,
            workType.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A work type with code '{workType.Code}' already exists." });
        }

        var maintenanceTypeError = await MasterDataReferenceResolver.ResolveMaintenanceTypeAsync(
            workType.MaintenanceType,
            maintenanceTypes,
            cancellationToken);

        if (maintenanceTypeError is not null)
        {
            return maintenanceTypeError;
        }

        var updated = await repository.UpdateAsync(
            id,
            workType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkType",
            workType.Id,
            workType.WorkTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkTypeResponse.FromEntity(workType));
    }

    private static async Task<IResult> UpdateWorkTypeStatusAsync(
        string id,
        UpdateWorkTypeStatusRequest request,
        IWorkTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workType is null)
        {
            return Results.NotFound();
        }

        workType.Status = request.Status;
        workType.UpdatedBy = request.UpdatedBy;
        workType.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            workType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkType",
            workType.Id,
            workType.WorkTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkTypeResponse.FromEntity(workType));
    }

    private static async Task<IResult> DeleteWorkTypeAsync(
        string id,
        IWorkTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work type id." });
        }

        var workType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workType is null)
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
            "WorkType",
            workType.Id,
            workType.WorkTypeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
