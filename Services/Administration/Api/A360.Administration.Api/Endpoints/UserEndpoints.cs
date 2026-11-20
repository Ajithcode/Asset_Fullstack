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
        LoginAccountClient loginAccounts,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Sign-in accounts are the source of truth for who exists; bring them in before listing
        var accounts = await loginAccounts.ListAsync(cancellationToken);
        if (accounts is not null)
        {
            await SyncSignInAccountsAsync(accounts, repository, loggerFactory.CreateLogger(nameof(UserEndpoints)), cancellationToken);
        }

        var users = await repository.GetUsersAsync(tenantId, clientId, cancellationToken);
        return Results.Ok(users.Select(UserResponse.FromEntity));
    }

    /// <summary>
    /// Keeps User Management in step with the sign-in accounts (UserAccount service): an account without a user here
    /// (self-registered, or created before User Management) gets one, linked by its account id; linked users take the
    /// account's role, active / suspended state and last sign-in. Profile fields and secondary roles stay as edited here.
    /// </summary>
    private static async Task SyncSignInAccountsAsync(
        IReadOnlyList<LoginAccountClient.LoginAccount> accounts,
        IUserRepository repository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var users = (await repository.GetUsersAsync(null, null, cancellationToken)).ToList();
        foreach (var account in accounts.Where(a => !a.IsDeleted && !string.IsNullOrWhiteSpace(a.Id) && !string.IsNullOrWhiteSpace(a.UserName)))
        {
            var username = account.UserName!.Trim().ToLowerInvariant();
            var email = (account.Email ?? string.Empty).Trim().ToLowerInvariant();
            var inactive = string.Equals(account.Status, "Inactive", StringComparison.OrdinalIgnoreCase);
            var user = users.FirstOrDefault(u => u.LoginAccountId == account.Id)
                ?? users.FirstOrDefault(u => string.IsNullOrWhiteSpace(u.LoginAccountId)
                    && (u.Username == username || (email.Length > 0 && u.Email == email)));

            try
            {
                if (user is null)
                {
                    var phone = account.ContactNo?.Trim();
                    user = new A360.Administration.Domain.Entities.User
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Name = string.IsNullOrWhiteSpace(account.ShortName) ? account.UserName!.Trim() : account.ShortName.Trim(),
                        Username = username,
                        Email = email,
                        Phone = string.IsNullOrWhiteSpace(phone) || phone == "N/A" ? null : phone,
                        PrimaryRole = account.RoleName ?? "User",
                        AccessScope = "All Locations",
                        // Every sign-in uses the authenticator app
                        MfaEnabled = true,
                        Status = inactive ? "Suspended" : "Active",
                        LastLoginAt = account.LastLogin,
                        LoginAccountId = account.Id,
                        CreatedBy = "sign-in account",
                        CreatedAt = account.CreatedAt ?? DateTime.UtcNow,
                        TenantId = account.TenantId,
                        ClientId = account.ClientId,
                        IsDeleted = false
                    };
                    users.Add(await repository.CreateAsync(user, cancellationToken));
                    continue;
                }

                var changed = user.LoginAccountId != account.Id;
                user.LoginAccountId = account.Id;
                if (account.RoleName is not null && !string.Equals(user.PrimaryRole, account.RoleName, StringComparison.OrdinalIgnoreCase))
                {
                    user.SecondaryRoles.RemoveAll(role => string.Equals(role, account.RoleName, StringComparison.OrdinalIgnoreCase));
                    user.PrimaryRole = account.RoleName;
                    changed = true;
                }

                var status = inactive ? "Suspended" : string.Equals(user.Status, "Suspended", StringComparison.OrdinalIgnoreCase) ? "Active" : user.Status;
                if (!string.Equals(user.Status, status, StringComparison.Ordinal))
                {
                    user.Status = status;
                    changed = true;
                }

                if (account.LastLogin is not null && user.LastLoginAt != account.LastLogin)
                {
                    user.LastLoginAt = account.LastLogin;
                    changed = true;
                }

                if (changed)
                {
                    await repository.UpdateAsync(user.Id, user, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One bad record must not hide the whole list
                logger.LogWarning(exception, "Could not sync sign-in account {AccountId} into User Management.", account.Id);
            }
        }
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

        // Without a User Management record the sign-in role applies (same roles as Role & Access Control)
        var names = user is null ? new List<string> { signInRole ?? "User" } : RoleNames(user);
        var tenantId = user?.TenantId ?? principal.FindFirst(A360ClaimTypes.TenantId)?.Value;
        var roles = (await roleRepository.GetByNamesAsync(names, tenantId, cancellationToken))
            .Where(role => !string.Equals(role.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (user is null && roles.Count == 0)
        {
            return Results.Ok(new MyAccessResponse(
                displayName, shownEmail, names[0], names, FullAccess: false, Modules: DefaultModules, User: null));
        }

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

        var criticality = AssetCriticalityPermissions.Module;
        var access = modules
            .Where(entry => entry.Value.Contains("view") && !string.Equals(entry.Key, criticality, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new ModuleAccessResponse(entry.Key, entry.Value.ToList()))
            .ToList();

        // Asset Criticality is a sub-menu of Assets and follows the role's Assets permissions; actions stored for the
        // AssetCriticality module itself are ignored. Always listed (possibly with no actions) so the gateway and the
        // Asset service never fall back to the role-name defaults (AssetCriticalityPermissions.RoleDefaults).
        access.Add(new ModuleAccessResponse(criticality, CriticalityFromAssets(modules.GetValueOrDefault("Assets"))));

        return Results.Ok(new MyAccessResponse(
            displayName, shownEmail, user?.PrimaryRole ?? names[0], names, FullAccess: false, Modules: access,
            User: user is null ? null : UserResponse.FromEntity(user)));
    }

    /// <summary>
    /// Assets View -> View; Assets Create or Edit -> Assess; Assets Approve -> Approve, Approve Critical, Configure.
    /// Nothing without Assets View.
    /// </summary>
    private static List<string> CriticalityFromAssets(IReadOnlySet<string>? assets)
    {
        if (assets is null || !assets.Contains("view"))
        {
            return [];
        }

        var granted = new List<string> { AssetCriticalityPermissions.View };
        if (assets.Contains("create") || assets.Contains("edit"))
        {
            granted.Add(AssetCriticalityPermissions.Assess);
        }

        if (assets.Contains("approve"))
        {
            granted.AddRange([AssetCriticalityPermissions.Approve, AssetCriticalityPermissions.ApproveCritical, AssetCriticalityPermissions.Configure]);
        }

        return granted;
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
