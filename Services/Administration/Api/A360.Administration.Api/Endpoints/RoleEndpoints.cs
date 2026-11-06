using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class RoleEndpoints
{
    private const string EntityType = "Role";

    public static RouteGroupBuilder MapRoleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/roles").WithTags("Roles");

        group.MapGet("", GetRolesAsync).WithName("GetRoles");
        group.MapGet("/{roleId}", GetRoleByIdAsync).WithName("GetRoleById");
        group.MapPost("", CreateRoleAsync).WithName("CreateRole");
        group.MapPut("/{roleId}", UpdateRoleAsync).WithName("UpdateRole");
        group.MapDelete("/{roleId}", DeleteRoleAsync).WithName("DeleteRole");

        return group;
    }

    private static async Task<IResult> UpdateRoleAsync(
        string roleId,
        UpdateRoleRequest request,
        IRoleRepository repository,
        IUserRepository userRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetByRoleIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var oldName = role.RoleName;
        request.ApplyTo(role, currentUserAccessor.GetCurrentUser());

        if (await repository.RoleCodeExistsAsync(role.RoleCode, role.TenantId, cancellationToken, exceptRoleId: role.RoleId))
        {
            return Results.Conflict(new { message = $"Role code '{role.RoleCode}' already exists" });
        }

        if (!await repository.UpdateAsync(role.Id, role, cancellationToken))
        {
            return Results.NotFound();
        }

        // Users keep their roles by name, so a rename follows through to them
        if (!string.Equals(oldName, role.RoleName, StringComparison.Ordinal))
        {
            await userRepository.RenameRoleAsync(oldName, role.RoleName, role.TenantId, cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Updated, cancellationToken);

        return Results.Ok(RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> DeleteRoleAsync(
        string roleId,
        IRoleRepository repository,
        IUserRepository userRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetByRoleIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        var assigned = await userRepository.CountWithRoleAsync(role.RoleName, role.TenantId, cancellationToken);
        if (assigned > 0)
        {
            return Results.Conflict(new { message = $"{assigned} user{(assigned == 1 ? " has" : "s have")} this role. Give them another role first." });
        }

        // Soft delete: the record stays for the audit trail
        role.IsDeleted = true;
        role.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName ?? role.UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(role.Id, role, cancellationToken);

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetRolesAsync(
        string? tenantId,
        string? clientId,
        string? roleType,
        string? status,
        IRoleRepository repository,
        CancellationToken cancellationToken)
    {
        var roles = await repository.GetAsync(tenantId, clientId, roleType, status, cancellationToken);

        return Results.Ok(roles.Select(RoleResponse.FromEntity));
    }

    private static async Task<IResult> GetRoleByIdAsync(
        string roleId,
        IRoleRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetByRoleIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Viewed, cancellationToken);

        return Results.Ok(RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        IRoleRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.RoleCodeExistsAsync(role.RoleCode, role.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Role code '{role.RoleCode}' already exists" });
        }

        role = await repository.CreateAsync(role, cancellationToken);

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/roles/{role.RoleId}", RoleResponse.FromEntity(role));
    }
}
