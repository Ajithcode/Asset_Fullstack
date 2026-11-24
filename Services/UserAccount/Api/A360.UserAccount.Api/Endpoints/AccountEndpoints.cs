using System.Security.Claims;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;
using A360.UserAccount.Api.Contracts;
using A360.UserAccount.Api.Security;
using A360.UserAccount.Repository.Repositories;
using Microsoft.Extensions.Caching.Memory;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Api.Endpoints;

/// <summary>
/// Password change (own) and reset (administrator). Exposed through the API gateway on these two routes only; nothing
/// here ever records or returns a password except the one-time temporary password handed to the administrator.
/// </summary>
public static class AccountEndpoints
{
    private const string EventEntityType = "User";

    /// <summary>Permission-matrix right an administrator needs to reset someone's password (as for editing users).</summary>
    private const string ResetModule = "Administration";
    private const string ResetAction = "edit";

    /// <summary>Wrong current passwords allowed per user in <see cref="FailureWindow"/> before changes are refused.</summary>
    private const int MaxFailures = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/account").WithTags("Account").RequireAuthorization();

        group.MapPost("/change-password", ChangePasswordAsync).WithName("ChangePassword");
        group.MapPut("/users/{id}/password", ResetPasswordAsync).WithName("ResetUserPassword");

        return group;
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        PasswordHashingService passwordHashingService,
        TokenService tokenService,
        IMemoryCache cache,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var id = principal.FindFirstValue(A360ClaimTypes.Subject);
        var user = string.IsNullOrWhiteSpace(id) || !MongoObjectId.IsValid(id) ? null : await repository.GetByIdAsync(id, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            return Results.NotFound(new { message = "Your account was not found." });
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        {
            return Results.BadRequest(new { message = "Current and new password are required." });
        }

        var failureKey = "pwd-change-failures:" + user.Id;
        if (cache.TryGetValue(failureKey, out int failures) && failures >= MaxFailures)
        {
            await LogFailedAsync(eventLogger, user, "Refused: too many wrong current passwords; try again later.", cancellationToken);
            return Results.Json(new { message = "Too many wrong attempts. Try again in 15 minutes." }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        // 400, not 401: a 401 would make the browser think its session ended
        if (!passwordHashingService.Verify(request.CurrentPassword, user.LoginPassword))
        {
            cache.Set(failureKey, failures + 1, FailureWindow);
            await LogFailedAsync(eventLogger, user, "Current password was wrong.", cancellationToken);
            return Results.BadRequest(new { message = "The current password is not correct." });
        }

        var problem = PasswordPolicy.Problem(request.NewPassword);
        if (problem is not null)
        {
            await LogFailedAsync(eventLogger, user, "New password did not meet the password rules.", cancellationToken);
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = [problem] });
        }

        if (passwordHashingService.Verify(request.NewPassword, user.LoginPassword))
        {
            await LogFailedAsync(eventLogger, user, "New password was the same as the current one.", cancellationToken);
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = ["Choose a password different from the current one."] });
        }

        if (!await repository.SetPasswordAsync(user.Id, passwordHashingService.Hash(request.NewPassword), user.UserName, user.LoginPassword, cancellationToken))
        {
            return Results.Conflict(new { message = "Your password was changed meanwhile. Sign in again and retry." });
        }

        cache.Remove(failureKey);

        // Keep this browser's session (when its refresh token is sent and is the user's), sign out every other one
        string? keepHash = null;
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = tokenService.HashRefreshToken(request.RefreshToken.Trim());
            var current = await refreshTokenRepository.GetByTokenHashAsync(hash, cancellationToken);
            keepHash = current is not null && current.UserRefId == user.Id ? hash : null;
        }

        var ended = await refreshTokenRepository.RevokeOthersForUserAsync(user.Id, keepHash, cancellationToken);
        var details = $"Password changed by the user. {Sessions(ended)} signed out" + (keepHash is null ? "." : " (this session kept).");
        await eventLogger.LogDetailedAsync(EventEntityType, user.Id, user.UserName, EventAction.PasswordChanged, details, ToActor(user), cancellationToken);

        return Results.Ok(new ChangePasswordResponse("Your password has been changed.", ended));
    }

    private static async Task<IResult> ResetPasswordAsync(
        string id,
        ResetPasswordRequest? request,
        HttpContext httpContext,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        PasswordHashingService passwordHashingService,
        AdministrationAccessChecker accessChecker,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var allowed = await accessChecker.AllowsAsync(httpContext.Request.Headers.Authorization.ToString(), ResetModule, ResetAction, cancellationToken);
        if (allowed is null)
        {
            return Results.Json(new { message = "Your permissions could not be checked. Try again in a moment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (allowed == false)
        {
            return Results.Json(new { message = "Your role doesn't allow you to reset passwords. Ask an administrator for access." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid user id." });
        }

        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            return Results.NotFound(new { message = "This user has no sign-in account." });
        }

        var generated = string.IsNullOrEmpty(request?.NewPassword);
        var password = generated ? PasswordPolicy.GenerateTemporary() : request!.NewPassword!;
        var problem = PasswordPolicy.Problem(password);
        if (problem is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = [problem] });
        }

        var actor = currentUserAccessor.GetCurrentUser()?.UserName ?? "Administrator";
        if (!await repository.SetPasswordAsync(user.Id, passwordHashingService.Hash(password), actor, cancellationToken: cancellationToken))
        {
            return Results.NotFound(new { message = "This user has no sign-in account." });
        }

        // A sign-in started with the old password can't be finished, and every session ends
        await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
        var ended = await refreshTokenRepository.RevokeOthersForUserAsync(user.Id, null, cancellationToken);

        var how = generated ? "Temporary password generated and shown once to the administrator" : "New password set by the administrator";
        await eventLogger.LogDetailedAsync(EventEntityType, user.Id, user.UserName, EventAction.PasswordReset, $"{how}. {Sessions(ended)} signed out.", cancellationToken);

        return Results.Ok(new ResetPasswordResponse(
            generated ? "A temporary password was generated. Share it with the user securely; it is shown only once." : "The new password is active.",
            generated ? password : null,
            ended));
    }

    private static string Sessions(long count) => count == 1 ? "1 session" : $"{count} sessions";

    private static Task LogFailedAsync(IEventLogger eventLogger, UserEntity user, string details, CancellationToken cancellationToken) =>
        eventLogger.LogDetailedAsync(EventEntityType, user.Id, user.UserName, EventAction.PasswordChangeFailed, details, ToActor(user), cancellationToken);

    private static EventActor ToActor(UserEntity user) => new(user.Id, user.UserName, user.ClientId, user.TenantId);
}
