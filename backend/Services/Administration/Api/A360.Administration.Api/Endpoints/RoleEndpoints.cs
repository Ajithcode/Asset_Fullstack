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

        if (await repository.RoleCodeExistsAsync(role.RoleCode, role.TenantId, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Role code '{role.RoleCode}' already exists" });
        }

        role = await repository.CreateAsync(role, cancellationToken);

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/roles/{role.RoleId}", RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> UpdateRoleAsync(
        string roleId,
        UpdateRoleRequest request,
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

        var role = await repository.GetByRoleIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return NotFound(roleId);
        }

        request.ApplyTo(role, currentUserAccessor.GetCurrentUser());

        if (await repository.RoleCodeExistsAsync(role.RoleCode, role.TenantId, role.RoleId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Role code '{role.RoleCode}' already exists" });
        }

        if (!await repository.UpdateAsync(role.Id, role, cancellationToken))
        {
            return NotFound(roleId);
        }

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Updated, cancellationToken);

        return Results.Ok(RoleResponse.FromEntity(role));
    }

    // Soft delete: the role stays in the collection with IsDeleted set.
    private static async Task<IResult> DeleteRoleAsync(
        string roleId,
        IRoleRepository repository,
        IAccessPolicyRepository accessPolicyRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetByRoleIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return NotFound(roleId);
        }

        if (string.Equals(role.RoleType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = $"Role '{role.RoleName}' is a system role and can't be deleted." });
        }

        if (await accessPolicyRepository.AnyAssignedToRoleAsync(role.RoleId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Role '{role.RoleName}' is assigned to access policies. Remove it from them first." });
        }

        role.IsDeleted = true;
        role.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        role.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(role.Id, role, cancellationToken))
        {
            return NotFound(roleId);
        }

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string roleId)
    {
        return Results.NotFound(new { message = $"Role '{roleId}' was not found." });
    }
}
