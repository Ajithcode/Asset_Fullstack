using PermissionEntity = A360.MasterDataManagement.Domain.Entities.Permission;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class PermissionEndpoints
{
    public static RouteGroupBuilder MapPermissionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/permissions")
            .WithTags("Permissions");

        group.MapGet("", GetPermissionsAsync)
            .WithName("GetPermissions");

        group.MapGet("/{id}", GetPermissionByIdAsync)
            .WithName("GetPermissionById");

        group.MapPost("", CreatePermissionAsync)
            .WithName("CreatePermission");

        group.MapPut("/{id}", UpdatePermissionAsync)
            .WithName("UpdatePermission");

        group.MapPatch("/{id}/status", UpdatePermissionStatusAsync)
            .WithName("UpdatePermissionStatus");

        group.MapDelete("/{id}", DeletePermissionAsync)
            .WithName("DeletePermission");

        return group;
    }

    private static async Task<IResult> GetPermissionsAsync(
        string? search,
        string? status,
        string? moduleId,
        string? type,
        string? clientId,
        IPermissionRepository repository,
        CancellationToken cancellationToken)
    {
        var permissions = await repository.SearchAsync(
            new PermissionFilter(search, status, moduleId, type, clientId),
            cancellationToken);

        return Results.Ok(
            permissions.Select(PermissionResponse.FromEntity));
    }

    private static async Task<IResult> GetPermissionByIdAsync(
        string id,
        IPermissionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permission id." });
        }

        var permission = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permission is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Permission",
            permission.Id,
            permission.PermissionName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PermissionResponse.FromEntity(permission));
    }

    private static async Task<IResult> CreatePermissionAsync(
        CreatePermissionRequest request,
        IPermissionRepository repository,
        IModuleAccessRepository modules,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permission = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            permission.Code,
            permission.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A permission with code '{permission.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            permission,
            modules,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        permission = await repository.CreateAsync(
            permission,
            cancellationToken);

        await eventLogger.LogAsync(
            "Permission",
            permission.Id,
            permission.PermissionName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/permissions/{permission.Id}",
            PermissionResponse.FromEntity(permission));
    }

    private static async Task<IResult> UpdatePermissionAsync(
        string id,
        UpdatePermissionRequest request,
        IPermissionRepository repository,
        IModuleAccessRepository modules,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permission id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permission = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permission is null)
        {
            return Results.NotFound();
        }

        var previousName = permission.PermissionName;

        request.ApplyTo(permission);

        var existing = await repository.GetByCodeAsync(
            permission.Code,
            permission.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A permission with code '{permission.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            permission,
            modules,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            permission,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != permission.PermissionName)
        {
            await roles.UpdatePermissionNameAsync(
                id,
                permission.PermissionName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Permission",
            permission.Id,
            permission.PermissionName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PermissionResponse.FromEntity(permission));
    }

    private static async Task<IResult> UpdatePermissionStatusAsync(
        string id,
        UpdatePermissionStatusRequest request,
        IPermissionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permission id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permission = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permission is null)
        {
            return Results.NotFound();
        }

        permission.Status = request.Status;
        permission.UpdatedBy = request.UpdatedBy;
        permission.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            permission,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Permission",
            permission.Id,
            permission.PermissionName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PermissionResponse.FromEntity(permission));
    }

    private static async Task<IResult> DeletePermissionAsync(
        string id,
        IPermissionRepository repository,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permission id." });
        }

        var permission = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permission is null)
        {
            return Results.NotFound();
        }

        if (await roles.AnyWithPermissionAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Roles still grant this permission. Remove it from those roles first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Permission",
            permission.Id,
            permission.PermissionName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        PermissionEntity permission,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        var moduleError = await MasterDataReferenceResolver.ResolveModuleAccessAsync(
            permission.Module,
            modules,
            cancellationToken);

        if (moduleError is not null)
        {
            return moduleError;
        }

        return null;
    }
}
