using SystemUserEntity = A360.MasterDataManagement.Domain.Entities.SystemUser;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class SystemUserEndpoints
{
    public static RouteGroupBuilder MapSystemUserEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/users")
            .WithTags("SystemUsers");

        group.MapGet("", GetSystemUsersAsync)
            .WithName("GetSystemUsers");

        group.MapGet("/{id}", GetSystemUserByIdAsync)
            .WithName("GetSystemUserById");

        group.MapPost("", CreateSystemUserAsync)
            .WithName("CreateSystemUser");

        group.MapPut("/{id}", UpdateSystemUserAsync)
            .WithName("UpdateSystemUser");

        group.MapPatch("/{id}/status", UpdateSystemUserStatusAsync)
            .WithName("UpdateSystemUserStatus");

        group.MapDelete("/{id}", DeleteSystemUserAsync)
            .WithName("DeleteSystemUser");

        return group;
    }

    private static async Task<IResult> GetSystemUsersAsync(
        string? search,
        string? status,
        string? primaryRoleId,
        string? departmentId,
        string? clientId,
        ISystemUserRepository repository,
        CancellationToken cancellationToken)
    {
        var systemUsers = await repository.SearchAsync(
            new SystemUserFilter(search, status, primaryRoleId, departmentId, clientId),
            cancellationToken);

        return Results.Ok(
            systemUsers.Select(SystemUserResponse.FromEntity));
    }

    private static async Task<IResult> GetSystemUserByIdAsync(
        string id,
        ISystemUserRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid user id." });
        }

        var user = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SystemUser",
            user.Id,
            user.FullName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SystemUserResponse.FromEntity(user));
    }

    private static async Task<IResult> CreateSystemUserAsync(
        CreateSystemUserRequest request,
        ISystemUserRepository repository,
        IDepartmentRepository departments,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = request.ToEntity();

        if (!string.IsNullOrWhiteSpace(user.Code))
        {
            var sameCode = await repository.GetByCodeAsync(
                user.Code,
                user.ClientId,
                cancellationToken);

            if (sameCode is not null)
            {
                return Results.Conflict(
                    new { message = $"A user with code '{user.Code}' already exists." });
            }
        }

        var existing = await repository.GetByUsernameAsync(
            user.Username,
            user.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A user with username '{user.Username}' already exists." });
        }

        var sameEmail = await repository.GetByEmailAsync(
            user.Email,
            user.ClientId,
            cancellationToken);

        if (sameEmail is not null)
        {
            return Results.Conflict(
                new { message = $"A user with email '{user.Email}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            user,
            departments,
            roles,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        user = await repository.CreateAsync(
            user,
            cancellationToken);

        await eventLogger.LogAsync(
            "SystemUser",
            user.Id,
            user.FullName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/users/{user.Id}",
            SystemUserResponse.FromEntity(user));
    }

    private static async Task<IResult> UpdateSystemUserAsync(
        string id,
        UpdateSystemUserRequest request,
        ISystemUserRepository repository,
        IDepartmentRepository departments,
        IRoleRepository roles,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid user id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(user);

        if (!string.IsNullOrWhiteSpace(user.Code))
        {
            var sameCode = await repository.GetByCodeAsync(
                user.Code,
                user.ClientId,
                cancellationToken);

            if (sameCode is not null && sameCode.Id != id)
            {
                return Results.Conflict(
                    new { message = $"A user with code '{user.Code}' already exists." });
            }
        }

        var existing = await repository.GetByUsernameAsync(
            user.Username,
            user.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A user with username '{user.Username}' already exists." });
        }

        var sameEmail = await repository.GetByEmailAsync(
            user.Email,
            user.ClientId,
            cancellationToken);

        if (sameEmail is not null && sameEmail.Id != id)
        {
            return Results.Conflict(
                new { message = $"A user with email '{user.Email}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            user,
            departments,
            roles,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            user,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SystemUser",
            user.Id,
            user.FullName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SystemUserResponse.FromEntity(user));
    }

    private static async Task<IResult> UpdateSystemUserStatusAsync(
        string id,
        UpdateSystemUserStatusRequest request,
        ISystemUserRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid user id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        user.Status = request.Status;
        user.UpdatedBy = request.UpdatedBy;
        user.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            user,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SystemUser",
            user.Id,
            user.FullName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SystemUserResponse.FromEntity(user));
    }

    private static async Task<IResult> DeleteSystemUserAsync(
        string id,
        ISystemUserRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid user id." });
        }

        var user = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (user is null)
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
            "SystemUser",
            user.Id,
            user.FullName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        SystemUserEntity user,
        IDepartmentRepository departments,
        IRoleRepository roles,
        CancellationToken cancellationToken)
    {
        var departmentError = await MasterDataReferenceResolver.ResolveDepartmentAsync(
            user.Department,
            departments,
            cancellationToken);

        if (departmentError is not null)
        {
            return departmentError;
        }

        var primaryRoleError = await MasterDataReferenceResolver.ResolveRoleAsync(
            user.PrimaryRole,
            roles,
            cancellationToken);

        if (primaryRoleError is not null)
        {
            return primaryRoleError;
        }

        foreach (var additionalRole in user.AdditionalRoles)
        {
            var additionalRoleError = await MasterDataReferenceResolver.ResolveRoleAsync(
                additionalRole,
                roles,
                cancellationToken);

            if (additionalRoleError is not null)
            {
                return additionalRoleError;
            }
        }

        return null;
    }
}
