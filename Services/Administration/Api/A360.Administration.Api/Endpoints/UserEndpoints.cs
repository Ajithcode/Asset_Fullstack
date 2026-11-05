using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Administration.Api.Accounts;
using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;

namespace A360.Administration.Api.Endpoints;

public static class UserEndpoints
{
    private const string EntityType = "User";

    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users");

        group.MapGet("", GetUsersAsync).WithName("GetUsers");
        group.MapGet("/me", GetMyAccessAsync).WithName("GetMyAccess");
        group.MapGet("/{userId}", GetUserByIdAsync).WithName("GetUserById");
        group.MapPost("", CreateUserAsync).WithName("CreateUser");
        group.MapPut("/{userId}", UpdateUserAsync).WithName("UpdateUser");
        group.MapDelete("/{userId}", DeleteUserAsync).WithName("DeleteUser");

        return group;
    }

    private static async Task<IResult> GetUsersAsync(
        string? tenantId,
        string? clientId,
        IUserRepository repository,
        CancellationToken cancellationToken)
    {
        var users = await repository.GetUsersAsync(tenantId, clientId, cancellationToken);
        return Results.Ok(users.Select(UserResponse.FromEntity));
    }

    /// <summary>UserAccount role whose holders see every module.</summary>
    private const string SignInAdministratorRoleId = "R-ADMINISTRATOR";

    /// <summary>What a signed-in user without a User Management record may open.</summary>
    private static readonly ModuleAccessResponse[] DefaultModules = [new("Dashboard", ["view"])];

    private static readonly string[] Granted = ["full", "partial"];

    /// <summary>
    /// The signed-in user's profile and module access. Sign-in administrators get full access; users with a User Management
    /// record get the union of what their active primary and secondary roles allow; anyone else gets the dashboard only.
    /// </summary>
    private static async Task<IResult> GetMyAccessAsync(
        HttpContext httpContext,
        IUserRepository repository,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var signInName = principal.FindFirst(A360ClaimTypes.UserName)?.Value ?? string.Empty;
        var email = principal.FindFirst(A360ClaimTypes.Email)?.Value ?? string.Empty;
        var signInRole = principal.FindFirst(A360ClaimTypes.Role)?.Value;
        var isAdministrator = string.Equals(principal.FindFirst(A360ClaimTypes.RoleId)?.Value, SignInAdministratorRoleId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(signInRole, "Administrator", StringComparison.OrdinalIgnoreCase);

        var user = await repository.GetBySignInNameAsync(email, cancellationToken)
            ?? await repository.GetBySignInNameAsync(signInName, cancellationToken);

        var displayName = user?.Name ?? signInName;
        var shownEmail = user?.Email ?? email;

        if (isAdministrator)
        {
            return Results.Ok(new MyAccessResponse(
                displayName, shownEmail, user?.PrimaryRole ?? "Administrator", user is null ? ["Administrator"] : RoleNames(user),
                FullAccess: true, Modules: [], User: user is null ? null : UserResponse.FromEntity(user)));
        }

        if (user is null)
        {
            return Results.Ok(new MyAccessResponse(
                displayName, shownEmail, signInRole ?? "User", [signInRole ?? "User"], FullAccess: false, Modules: DefaultModules, User: null));
        }

        var names = RoleNames(user);
        var roles = (await roleRepository.GetByNamesAsync(names, user.TenantId, cancellationToken))
            .Where(role => !string.Equals(role.Status, "Inactive", StringComparison.OrdinalIgnoreCase));

        // Union across roles: an action is allowed when any role grants it (full or partial)
        var modules = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            foreach (var (module, actions) in role.Permissions)
            {
                foreach (var (action, state) in actions)
                {
                    if (!Granted.Contains(state, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!modules.TryGetValue(module, out var set))
                    {
                        modules[module] = set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    set.Add(action);
                }
            }
        }

        var access = modules
            .Where(entry => entry.Value.Contains("view"))
            .Select(entry => new ModuleAccessResponse(entry.Key, entry.Value.ToList()))
            .ToList();

        return Results.Ok(new MyAccessResponse(
            displayName, shownEmail, user.PrimaryRole, names, FullAccess: false, Modules: access, User: UserResponse.FromEntity(user)));
    }

    private static List<string> RoleNames(A360.Administration.Domain.Entities.User user)
    {
        return new[] { user.PrimaryRole }
            .Concat(user.SecondaryRoles)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IResult> GetUserByIdAsync(
        string userId,
        IUserRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Viewed, cancellationToken);

        return Results.Ok(UserResponse.FromEntity(user));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        IUserRepository repository,
        LoginAccountClient loginAccounts,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.UsernameExistsAsync(user.Username, user.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Username '{user.Username}' already exists" });
        }

        if (await repository.EmailExistsAsync(user.Email, user.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Email '{user.Email}' already exists" });
        }

        // With a password, the sign-in account comes first so the user is never saved with a broken login.
        // Without one (invite / import) the user has no sign-in until a password is set on update.
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var login = await loginAccounts.CreateAsync(user, request.Password, cancellationToken);
            if (!login.Succeeded)
            {
                return Results.BadRequest(new { message = login.Error });
            }

            user.LoginAccountId = login.AccountId;
        }

        try
        {
            user = await repository.CreateAsync(user, cancellationToken);
        }
        catch when (user.LoginAccountId is not null)
        {
            await loginAccounts.DeleteAsync(user.LoginAccountId, CancellationToken.None);
            throw;
        }

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Created, cancellationToken);

        return Results.Created($"/api/users/{user.UserId}", UserResponse.FromEntity(user));
    }

    private static async Task<IResult> UpdateUserAsync(
        string userId,
        UpdateUserRequest request,
        IUserRepository repository,
        LoginAccountClient loginAccounts,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        request.ApplyTo(user, currentUserAccessor.GetCurrentUser());

        if (await repository.UsernameExistsAsync(user.Username, user.TenantId, cancellationToken, exceptUserId: user.UserId))
        {
            return Results.Conflict(new { message = $"Username '{user.Username}' already exists" });
        }

        if (await repository.EmailExistsAsync(user.Email, user.TenantId, cancellationToken, exceptUserId: user.UserId))
        {
            return Results.Conflict(new { message = $"Email '{user.Email}' already exists" });
        }

        var loginError = await SyncLoginAsync(user, request.Password, loginAccounts, cancellationToken);
        if (loginError is not null)
        {
            return Results.BadRequest(new { message = loginError });
        }

        if (!await repository.UpdateAsync(user.Id, user, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(UserResponse.FromEntity(user));
    }

    private static async Task<IResult> DeleteUserAsync(
        string userId,
        IUserRepository repository,
        LoginAccountClient loginAccounts,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        // Remove the sign-in account first so a deleted user can't keep signing in
        if (!string.IsNullOrWhiteSpace(user.LoginAccountId))
        {
            var login = await loginAccounts.DeleteAsync(user.LoginAccountId, cancellationToken);
            if (!login.Succeeded)
            {
                return Results.BadRequest(new { message = login.Error });
            }
        }

        // Soft delete: the record stays for the audit trail
        user.IsDeleted = true;
        user.LoginAccountId = null;
        user.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName ?? user.UpdatedBy;
        user.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(user.Id, user, cancellationToken);

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Brings the user's sign-in account in line with the edited user: updates it (and the password when one is given),
    /// creates it when a password is set for a user without one, and drops a link to an account that no longer exists.
    /// Returns an error message, or null when the account is in step.
    /// </summary>
    private static async Task<string?> SyncLoginAsync(
        A360.Administration.Domain.Entities.User user,
        string? password,
        LoginAccountClient loginAccounts,
        CancellationToken cancellationToken)
    {
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (!string.IsNullOrWhiteSpace(user.LoginAccountId))
        {
            var updated = await loginAccounts.UpdateAsync(user, user.LoginAccountId, password, cancellationToken);
            if (updated.Succeeded)
            {
                return null;
            }

            if (!updated.NotFound)
            {
                return updated.Error;
            }

            // The account was removed elsewhere: recreate it when there is a password, otherwise forget it
            user.LoginAccountId = null;
        }

        if (!hasPassword)
        {
            return null;
        }

        var created = await loginAccounts.CreateAsync(user, password!, cancellationToken);
        if (!created.Succeeded)
        {
            return created.Error;
        }

        user.LoginAccountId = created.AccountId;
        return null;
    }
}
