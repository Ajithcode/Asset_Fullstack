using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class MaintenanceTypeEndpoints
{
    public static RouteGroupBuilder MapMaintenanceTypeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/maintenance-types")
            .WithTags("MaintenanceTypes");

        group.MapGet("", GetMaintenanceTypesAsync)
            .WithName("GetMaintenanceTypes");

        group.MapGet("/{id}", GetMaintenanceTypeByIdAsync)
            .WithName("GetMaintenanceTypeById");

        group.MapPost("", CreateMaintenanceTypeAsync)
            .WithName("CreateMaintenanceType");

        group.MapPut("/{id}", UpdateMaintenanceTypeAsync)
            .WithName("UpdateMaintenanceType");

        group.MapPatch("/{id}/status", UpdateMaintenanceTypeStatusAsync)
            .WithName("UpdateMaintenanceTypeStatus");

        group.MapDelete("/{id}", DeleteMaintenanceTypeAsync)
            .WithName("DeleteMaintenanceType");

        return group;
    }

    private static async Task<IResult> GetMaintenanceTypesAsync(
        string? search,
        string? status,
        string? clientId,
        IMaintenanceTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var maintenanceTypes = await repository.SearchAsync(
            new MaintenanceTypeFilter(search, status, clientId),
            cancellationToken);

        return Results.Ok(
            maintenanceTypes.Select(MaintenanceTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetMaintenanceTypeByIdAsync(
        string id,
        IMaintenanceTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance type id." });
        }

        var maintenanceType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaintenanceType",
            maintenanceType.Id,
            maintenanceType.MaintenanceTypeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(MaintenanceTypeResponse.FromEntity(maintenanceType));
    }

    private static async Task<IResult> CreateMaintenanceTypeAsync(
        CreateMaintenanceTypeRequest request,
        IMaintenanceTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var maintenanceType = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            maintenanceType.Code,
            maintenanceType.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A maintenance type with code '{maintenanceType.Code}' already exists." });
        }

        maintenanceType = await repository.CreateAsync(
            maintenanceType,
            cancellationToken);

        await eventLogger.LogAsync(
            "MaintenanceType",
            maintenanceType.Id,
            maintenanceType.MaintenanceTypeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/maintenance-types/{maintenanceType.Id}",
            MaintenanceTypeResponse.FromEntity(maintenanceType));
    }

    private static async Task<IResult> UpdateMaintenanceTypeAsync(
        string id,
        UpdateMaintenanceTypeRequest request,
        IMaintenanceTypeRepository repository,
        IWorkTypeRepository workTypes,
        ITaskTemplateRepository taskTemplates,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var maintenanceType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceType is null)
        {
            return Results.NotFound();
        }

        var previousName = maintenanceType.MaintenanceTypeName;

        request.ApplyTo(maintenanceType);

        var existing = await repository.GetByCodeAsync(
            maintenanceType.Code,
            maintenanceType.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A maintenance type with code '{maintenanceType.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            maintenanceType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != maintenanceType.MaintenanceTypeName)
        {
            await workTypes.UpdateMaintenanceTypeNameAsync(
                id,
                maintenanceType.MaintenanceTypeName,
                cancellationToken);

            await taskTemplates.UpdateMaintenanceTypeNameAsync(
                id,
                maintenanceType.MaintenanceTypeName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "MaintenanceType",
            maintenanceType.Id,
            maintenanceType.MaintenanceTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(MaintenanceTypeResponse.FromEntity(maintenanceType));
    }

    private static async Task<IResult> UpdateMaintenanceTypeStatusAsync(
        string id,
        UpdateMaintenanceTypeStatusRequest request,
        IMaintenanceTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var maintenanceType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceType is null)
        {
            return Results.NotFound();
        }

        maintenanceType.Status = request.Status;
        maintenanceType.UpdatedBy = request.UpdatedBy;
        maintenanceType.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            maintenanceType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaintenanceType",
            maintenanceType.Id,
            maintenanceType.MaintenanceTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(MaintenanceTypeResponse.FromEntity(maintenanceType));
    }

    private static async Task<IResult> DeleteMaintenanceTypeAsync(
        string id,
        IMaintenanceTypeRepository repository,
        IWorkTypeRepository workTypes,
        ITaskTemplateRepository taskTemplates,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance type id." });
        }

        var maintenanceType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceType is null)
        {
            return Results.NotFound();
        }

        if (await workTypes.AnyWithMaintenanceTypeAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Work types still use this maintenance type. Change or delete them first." });
        }

        if (await taskTemplates.AnyWithMaintenanceTypeAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Task templates still use this maintenance type. Change or delete them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaintenanceType",
            maintenanceType.Id,
            maintenanceType.MaintenanceTypeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
