using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class TaskTemplateEndpoints
{
    public static RouteGroupBuilder MapTaskTemplateEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/task-templates")
            .WithTags("TaskTemplates");

        group.MapGet("", GetTaskTemplatesAsync)
            .WithName("GetTaskTemplates");

        group.MapGet("/{id}", GetTaskTemplateByIdAsync)
            .WithName("GetTaskTemplateById");

        group.MapPost("", CreateTaskTemplateAsync)
            .WithName("CreateTaskTemplate");

        group.MapPut("/{id}", UpdateTaskTemplateAsync)
            .WithName("UpdateTaskTemplate");

        group.MapPatch("/{id}/status", UpdateTaskTemplateStatusAsync)
            .WithName("UpdateTaskTemplateStatus");

        group.MapDelete("/{id}", DeleteTaskTemplateAsync)
            .WithName("DeleteTaskTemplate");

        return group;
    }

    private static async Task<IResult> GetTaskTemplatesAsync(
        string? search,
        string? status,
        string? maintenanceTypeId,
        string? clientId,
        ITaskTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var taskTemplates = await repository.SearchAsync(
            new TaskTemplateFilter(search, status, maintenanceTypeId, clientId),
            cancellationToken);

        return Results.Ok(
            taskTemplates.Select(TaskTemplateResponse.FromEntity));
    }

    private static async Task<IResult> GetTaskTemplateByIdAsync(
        string id,
        ITaskTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task template id." });
        }

        var taskTemplate = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskTemplate is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "TaskTemplate",
            taskTemplate.Id,
            taskTemplate.TemplateName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(TaskTemplateResponse.FromEntity(taskTemplate));
    }

    private static async Task<IResult> CreateTaskTemplateAsync(
        CreateTaskTemplateRequest request,
        ITaskTemplateRepository repository,
        IMaintenanceTypeRepository maintenanceTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskTemplate = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            taskTemplate.Code,
            taskTemplate.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A task template with code '{taskTemplate.Code}' already exists." });
        }

        var maintenanceTypeError = await MasterDataReferenceResolver.ResolveMaintenanceTypeAsync(
            taskTemplate.MaintenanceType,
            maintenanceTypes,
            cancellationToken);

        if (maintenanceTypeError is not null)
        {
            return maintenanceTypeError;
        }

        taskTemplate = await repository.CreateAsync(
            taskTemplate,
            cancellationToken);

        await eventLogger.LogAsync(
            "TaskTemplate",
            taskTemplate.Id,
            taskTemplate.TemplateName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/task-templates/{taskTemplate.Id}",
            TaskTemplateResponse.FromEntity(taskTemplate));
    }

    private static async Task<IResult> UpdateTaskTemplateAsync(
        string id,
        UpdateTaskTemplateRequest request,
        ITaskTemplateRepository repository,
        IMaintenanceTypeRepository maintenanceTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task template id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskTemplate = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskTemplate is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(taskTemplate);

        var existing = await repository.GetByCodeAsync(
            taskTemplate.Code,
            taskTemplate.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A task template with code '{taskTemplate.Code}' already exists." });
        }

        var maintenanceTypeError = await MasterDataReferenceResolver.ResolveMaintenanceTypeAsync(
            taskTemplate.MaintenanceType,
            maintenanceTypes,
            cancellationToken);

        if (maintenanceTypeError is not null)
        {
            return maintenanceTypeError;
        }

        var updated = await repository.UpdateAsync(
            id,
            taskTemplate,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "TaskTemplate",
            taskTemplate.Id,
            taskTemplate.TemplateName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TaskTemplateResponse.FromEntity(taskTemplate));
    }

    private static async Task<IResult> UpdateTaskTemplateStatusAsync(
        string id,
        UpdateTaskTemplateStatusRequest request,
        ITaskTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task template id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskTemplate = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskTemplate is null)
        {
            return Results.NotFound();
        }

        taskTemplate.Status = request.Status;
        taskTemplate.UpdatedBy = request.UpdatedBy;
        taskTemplate.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            taskTemplate,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "TaskTemplate",
            taskTemplate.Id,
            taskTemplate.TemplateName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TaskTemplateResponse.FromEntity(taskTemplate));
    }

    private static async Task<IResult> DeleteTaskTemplateAsync(
        string id,
        ITaskTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task template id." });
        }

        var taskTemplate = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskTemplate is null)
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
            "TaskTemplate",
            taskTemplate.Id,
            taskTemplate.TemplateName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
