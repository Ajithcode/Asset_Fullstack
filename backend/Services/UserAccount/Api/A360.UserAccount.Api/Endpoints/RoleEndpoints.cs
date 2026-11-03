using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.UserAccount.Api.Contracts;
using A360.UserAccount.Api.Validation;
using A360.UserAccount.Repository.Repositories;

namespace A360.UserAccount.Api.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/roles").WithTags("Roles");

        group.MapGet("", GetRolesAsync).WithName("GetRoles");
        group.MapGet("/by-role-id/{roleId}", GetRoleByRoleIdAsync).WithName("GetRoleByRoleId");
        group.MapGet("/{id}", GetRoleByIdAsync).WithName("GetRoleById");
        group.MapPost("", CreateRoleAsync).WithName("CreateRole");
        group.MapPut("/{id}", UpdateRoleAsync).WithName("UpdateRole");
        group.MapDelete("/{id}", DeleteRoleAsync).WithName("DeleteRole");

        return group;
    }

    private static async Task<IResult> GetRolesAsync(
        IRoleRepository repository,
        CancellationToken cancellationToken)
    {
        var roles = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(roles.Select(RoleResponse.FromEntity));
    }

    private static async Task<IResult> GetRoleByRoleIdAsync(
        string roleId,
        IRoleRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return Results.BadRequest(new { message = "Role id is required." });
        }

        var role = await repository.GetByRoleIdAsync(roleId.Trim(), cancellationToken);
        return role is null ? Results.NotFound() : Results.Ok(RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> GetRoleByIdAsync(
        string id,
        IRoleRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Role", role.Id, role.RoleName, EventAction.Viewed, cancellationToken);
        return Results.Ok(RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        IRoleRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.CreateAsync(request.ToEntity(), cancellationToken);
        await eventLogger.LogAsync("Role", role.Id, role.RoleName, EventAction.Created, cancellationToken);
        return Results.Created($"/api/roles/{role.Id}", RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> UpdateRoleAsync(
        string id,
        UpdateRoleRequest request,
        IRoleRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid role id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(role);

        var updated = await repository.UpdateAsync(id, role, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Role", role.Id, role.RoleName, EventAction.Updated, cancellationToken);
        return Results.Ok(RoleResponse.FromEntity(role));
    }

    private static async Task<IResult> DeleteRoleAsync(
        string id,
        IRoleRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Role", role.Id, role.RoleName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
