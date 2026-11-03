using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ModuleAccessEndpoints
{
    public static RouteGroupBuilder MapModuleAccessEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/modules")
            .WithTags("ModuleAccesses");

        group.MapGet("", GetModuleAccessesAsync)
            .WithName("GetModuleAccesses");

        group.MapGet("/{id}", GetModuleAccessByIdAsync)
            .WithName("GetModuleAccessById");

        group.MapPost("", CreateModuleAccessAsync)
            .WithName("CreateModuleAccess");

        group.MapPut("/{id}", UpdateModuleAccessAsync)
            .WithName("UpdateModuleAccess");

        group.MapPatch("/{id}/status", UpdateModuleAccessStatusAsync)
            .WithName("UpdateModuleAccessStatus");

        group.MapDelete("/{id}", DeleteModuleAccessAsync)
            .WithName("DeleteModuleAccess");

        return group;
    }

    private static async Task<IResult> GetModuleAccessesAsync(
        string? search,
        string? status,
        string? category,
        string? clientId,
        IModuleAccessRepository repository,
        CancellationToken cancellationToken)
    {
        var moduleAccesses = await repository.SearchAsync(
            new ModuleAccessFilter(search, status, category, clientId),
            cancellationToken);

        return Results.Ok(
            moduleAccesses.Select(ModuleAccessResponse.FromEntity));
    }

    private static async Task<IResult> GetModuleAccessByIdAsync(
        string id,
        IModuleAccessRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid module id." });
        }

        var moduleAccess = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (moduleAccess is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ModuleAccess",
            moduleAccess.Id,
            moduleAccess.ModuleName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ModuleAccessResponse.FromEntity(moduleAccess));
    }

    private static async Task<IResult> CreateModuleAccessAsync(
        CreateModuleAccessRequest request,
        IModuleAccessRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var moduleAccess = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            moduleAccess.Code,
            moduleAccess.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A module with code '{moduleAccess.Code}' already exists." });
        }

        moduleAccess = await repository.CreateAsync(
            moduleAccess,
            cancellationToken);

        await eventLogger.LogAsync(
            "ModuleAccess",
            moduleAccess.Id,
            moduleAccess.ModuleName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/modules/{moduleAccess.Id}",
            ModuleAccessResponse.FromEntity(moduleAccess));
    }

    private static async Task<IResult> UpdateModuleAccessAsync(
        string id,
        UpdateModuleAccessRequest request,
        IModuleAccessRepository repository,
        IPermissionRepository permissions,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid module id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var moduleAccess = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (moduleAccess is null)
        {
            return Results.NotFound();
        }

        var previousName = moduleAccess.ModuleName;

        request.ApplyTo(moduleAccess);

        var existing = await repository.GetByCodeAsync(
            moduleAccess.Code,
            moduleAccess.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A module with code '{moduleAccess.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            moduleAccess,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != moduleAccess.ModuleName)
        {
            await permissions.UpdateModuleNameAsync(
                id,
                moduleAccess.ModuleName,
                cancellationToken);

            await roles.UpdateModuleNameAsync(
                id,
                moduleAccess.ModuleName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "ModuleAccess",
            moduleAccess.Id,
            moduleAccess.ModuleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ModuleAccessResponse.FromEntity(moduleAccess));
    }

    private static async Task<IResult> UpdateModuleAccessStatusAsync(
        string id,
        UpdateModuleAccessStatusRequest request,
        IModuleAccessRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid module id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var moduleAccess = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (moduleAccess is null)
        {
            return Results.NotFound();
        }

        moduleAccess.Status = request.Status;
        moduleAccess.UpdatedBy = request.UpdatedBy;
        moduleAccess.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            moduleAccess,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ModuleAccess",
            moduleAccess.Id,
            moduleAccess.ModuleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ModuleAccessResponse.FromEntity(moduleAccess));
    }

    private static async Task<IResult> DeleteModuleAccessAsync(
        string id,
        IModuleAccessRepository repository,
        IPermissionRepository permissions,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid module id." });
        }

        var moduleAccess = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (moduleAccess is null)
        {
            return Results.NotFound();
        }

        if (await permissions.AnyWithModuleAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Permissions still belong to this module. Delete or move them first." });
        }

        if (await roles.AnyWithModuleAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Roles still have access to this module. Remove it from those roles first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ModuleAccess",
            moduleAccess.Id,
            moduleAccess.ModuleName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
