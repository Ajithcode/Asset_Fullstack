using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/roles")
            .WithTags("Roles");

        group.MapGet("", GetRolesAsync)
            .WithName("GetRoles");

        group.MapGet("/{id}", GetRoleByIdAsync)
            .WithName("GetRoleById");

        group.MapPost("", CreateRoleAsync)
            .WithName("CreateRole");

        group.MapPut("/{id}", UpdateRoleAsync)
            .WithName("UpdateRole");

        group.MapPatch("/{id}/status", UpdateRoleStatusAsync)
            .WithName("UpdateRoleStatus");

        group.MapDelete("/{id}", DeleteRoleAsync)
            .WithName("DeleteRole");

        return group;
    }

    private static async Task<IResult> GetRolesAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        IRoleRepository repository,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var roles = await repository.SearchAsync(
            new RoleFilter(search, status, type, clientId),
            cancellationToken);

        var userCounts = await users.CountByPrimaryRoleAsync(
            roles.Select(x => x.Id),
            cancellationToken);

        return Results.Ok(
            roles.Select(x => RoleResponse.FromEntity(
                x,
                userCounts.GetValueOrDefault(x.Id))));
    }

    private static async Task<IResult> GetRoleByIdAsync(
        string id,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = request.ToEntity();

        if (!string.IsNullOrWhiteSpace(request.CopyFromRoleId))
        {
            var source = MongoObjectId.IsValid(request.CopyFromRoleId)
                ? await repository.GetByIdAsync(request.CopyFromRoleId, cancellationToken)
                : null;

            if (source is null)
            {
                return Results.BadRequest(
                    new { message = $"Role '{request.CopyFromRoleId}' to copy from was not found." });
            }

            if (request.Permissions is null)
            {
                role.Permissions = source.Permissions
                    .Select(x => new MasterDataReference { Id = x.Id, Name = x.Name })
                    .ToList();
            }

            if (request.ModuleAccess is null)
            {
                role.ModuleAccess = source.ModuleAccess
                    .Select(x => new MasterDataReference { Id = x.Id, Name = x.Name })
                    .ToList();
            }
        }

        var existing = await repository.GetByCodeAsync(
            role.Code,
            role.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A role with code '{role.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            role,
            permissions,
            modules,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        role = await repository.CreateAsync(
            role,
            cancellationToken);

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/roles/{role.Id}",
            await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> UpdateRoleAsync(
        string id,
        UpdateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        var previousName = role.RoleName;

        request.ApplyTo(role);

        var existing = await repository.GetByCodeAsync(
            role.Code,
            role.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A role with code '{role.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            role,
            permissions,
            modules,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            role,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != role.RoleName)
        {
            await users.UpdatePrimaryRoleNameAsync(
                id,
                role.RoleName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> UpdateRoleStatusAsync(
        string id,
        UpdateRoleStatusRequest request,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        role.Status = request.Status;
        role.UpdatedBy = request.UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            role,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> DeleteRoleAsync(
        string id,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        if (await users.AnyWithPrimaryRoleAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Users still have this as their primary role. Change their role first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        RoleEntity role,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        var permissionsError = await MasterDataReferenceResolver.ResolvePermissionListAsync(
            role.Permissions,
            permissions,
            cancellationToken);

        if (permissionsError is not null)
        {
            return permissionsError;
        }

        var moduleAccessError = await MasterDataReferenceResolver.ResolveModuleAccessListAsync(
            role.ModuleAccess,
            modules,
            cancellationToken);

        if (moduleAccessError is not null)
        {
            return moduleAccessError;
        }

        return null;
    }

    private static async Task<RoleResponse> ToResponseAsync(
        RoleEntity role,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var userCounts = await users.CountByPrimaryRoleAsync(
            [role.Id],
            cancellationToken);

        return RoleResponse.FromEntity(
            role,
            userCounts.GetValueOrDefault(role.Id));
    }
}
