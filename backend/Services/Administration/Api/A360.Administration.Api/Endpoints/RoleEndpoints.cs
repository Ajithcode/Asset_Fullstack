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

        if (await repository.RoleCodeExistsAsync(role.RoleCode, role.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Role code '{role.RoleCode}' already exists" });
        }

        role = await repository.CreateAsync(role, cancellationToken);

        await eventLogger.LogAsync(EntityType, role.Id, role.RoleName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/roles/{role.RoleId}", RoleResponse.FromEntity(role));
    }
}
