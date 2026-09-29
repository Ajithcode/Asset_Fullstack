using System.Net;
using System.Security.Claims;
using A360.Domain.Entities;
using A360.Email;
using A360.Repository.Activity;
using A360.Security;
using A360.UserAccount.Api.Contracts;
using A360.UserAccount.Api.Email;
using A360.UserAccount.Api.Security;
using A360.UserAccount.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Api.Endpoints;

public static class AuthEndpoints
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";
    private const string InvalidOtpMessage = "Invalid or expired OTP.";
    private const string InvalidRefreshTokenMessage = "Invalid or expired refresh token.";
    private const string EventEntityType = "User";
    private const int MaxLoggedUserNameLength = 100;

    // Verified against when the username is unknown, so both failure paths take the same time.
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHashingService().Hash(Guid.NewGuid().ToString()));

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous();
        group.MapPost("/verify-otp", VerifyOtpAsync).WithName("VerifyOtp").AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).WithName("RefreshToken").AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).WithName("Logout").AllowAnonymous();
        group.MapGet("/me", GetCurrentUserAsync).WithName("GetCurrentUser").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserRepository repository,
        PasswordHashingService passwordHashingService,
        OneTimeCodeService oneTimeCodeService,
        IEmailService emailService,
        IEventLogger eventLogger,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return Results.BadRequest(new { message = "Username and password are required." });
        }

        var user = await FindUniqueUserAsync(repository, request.UserName, cancellationToken);
        var passwordValid = passwordHashingService.Verify(request.Password, user?.LoginPassword ?? DummyPasswordHash.Value);

        if (user is null)
        {
            await LogAttemptAsync(eventLogger, request.UserName, EventAction.LoginFailed, cancellationToken);
            return Unauthorized(InvalidCredentialsMessage);
        }

        if (!passwordValid)
        {
            await LogAsync(eventLogger, user, EventAction.LoginFailed, cancellationToken);
            return Unauthorized(InvalidCredentialsMessage);
        }

        if (!CanSignIn(user))
        {
            await LogAsync(eventLogger, user, EventAction.LoginBlocked, cancellationToken);
            return Results.Json(new { message = "This account is inactive." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            await LogAsync(eventLogger, user, EventAction.LoginBlocked, cancellationToken);
            return Results.Json(new { message = "No email address is registered for this account." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var code = oneTimeCodeService.GenerateCode();
        var expiration = DateTime.UtcNow.Add(OneTimeCodeService.CodeLifetime);
        await repository.SetTwoFactorCodeAsync(user.Id, oneTimeCodeService.Hash(code), expiration, cancellationToken);

        try
        {
            await emailService.SendEmailAsync(
                user.Email,
                "Your Asset360 login code",
                BuildOtpEmailBody(user.UserName, code),
                cancellationToken,
                isHtml: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(nameof(AuthEndpoints)).LogError(exception, "Failed to send login OTP to user {UserId}.", user.Id);
            await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
            await LogAsync(eventLogger, user, EventAction.OtpSendFailed, cancellationToken);

            var message = exception is EmailNotConfiguredException
                ? "Email service is not configured. Please contact the administrator."
                : "Could not send the OTP email. Please try again later.";

            return Results.Json(new { message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await LogAsync(eventLogger, user, EventAction.OtpSent, cancellationToken);
        return Results.Ok(new LoginResponse(
            TwoFactorRequired: true,
            Message: "An OTP has been sent to your registered email address.",
            MaskedEmail: MaskEmail(user.Email),
            OtpExpiresInSeconds: (int)OneTimeCodeService.CodeLifetime.TotalSeconds));
    }

    private static async Task<IResult> VerifyOtpAsync(
        VerifyOtpRequest request,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        OneTimeCodeService oneTimeCodeService,
        TokenService tokenService,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return Results.BadRequest(new { message = "Username and OTP are required." });
        }

        var user = await FindUniqueUserAsync(repository, request.UserName, cancellationToken);
        if (user is null)
        {
            await LogAttemptAsync(eventLogger, request.UserName, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidOtpMessage);
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorCode)
            || user.TwoFactorExpiration is null
            || user.TwoFactorExpiration <= DateTime.UtcNow)
        {
            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidOtpMessage);
        }

        if (user.TwoFactorAttempts >= OneTimeCodeService.MaxAttempts)
        {
            await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
            await LogAsync(eventLogger, user, EventAction.OtpLocked, cancellationToken);
            return Unauthorized("Too many invalid attempts. Please log in again.");
        }

        if (!oneTimeCodeService.Matches(request.Otp, user.TwoFactorCode))
        {
            var attempts = await repository.RegisterFailedTwoFactorAttemptAsync(user.Id, cancellationToken);
            if (attempts >= OneTimeCodeService.MaxAttempts)
            {
                await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
                await LogAsync(eventLogger, user, EventAction.OtpLocked, cancellationToken);
                return Unauthorized("Too many invalid attempts. Please log in again.");
            }

            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidOtpMessage);
        }

        if (!CanSignIn(user))
        {
            await LogAsync(eventLogger, user, EventAction.LoginBlocked, cancellationToken);
            return Unauthorized(InvalidOtpMessage);
        }

        if (!await repository.CompleteTwoFactorLoginAsync(user.Id, user.TwoFactorCode, cancellationToken))
        {
            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidOtpMessage);
        }

        var response = await IssueTokensAsync(user, refreshTokenRepository, tokenService, "Login successful.", cancellationToken);
        await LogAsync(eventLogger, user, EventAction.LoggedIn, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new { message = "Refresh token is required." });
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken.Trim());
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is null)
        {
            await LogAttemptAsync(eventLogger, null, EventAction.TokenRefreshFailed, cancellationToken);
            return Unauthorized(InvalidRefreshTokenMessage);
        }

        var user = await repository.GetByIdAsync(storedToken.UserRefId, cancellationToken);

        if (storedToken.RevokedAt is not null)
        {
            // A revoked token being replayed means it may have been stolen: end every session of this user.
            await refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserRefId, cancellationToken);
            await LogTokenOwnerAsync(eventLogger, user, storedToken.UserRefId, EventAction.TokenReuseDetected, cancellationToken);
            return Unauthorized(InvalidRefreshTokenMessage);
        }

        if (storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            await LogTokenOwnerAsync(eventLogger, user, storedToken.UserRefId, EventAction.TokenRefreshFailed, cancellationToken);
            return Unauthorized(InvalidRefreshTokenMessage);
        }

        if (user is null || !CanSignIn(user))
        {
            await refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserRefId, cancellationToken);
            await LogTokenOwnerAsync(eventLogger, user, storedToken.UserRefId, EventAction.TokenRefreshFailed, cancellationToken);
            return Unauthorized(InvalidRefreshTokenMessage);
        }

        var newRefreshToken = tokenService.CreateRefreshToken();
        if (!await refreshTokenRepository.RevokeAsync(tokenHash, newRefreshToken.TokenHash, cancellationToken))
        {
            await LogAsync(eventLogger, user, EventAction.TokenRefreshFailed, cancellationToken);
            return Unauthorized(InvalidRefreshTokenMessage);
        }

        var response = await IssueTokensAsync(user, refreshTokenRepository, tokenService, "Token refreshed.", cancellationToken, newRefreshToken);
        await LogAsync(eventLogger, user, EventAction.TokenRefreshed, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequest request,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new { message = "Refresh token is required." });
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken.Trim());
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is not null && await refreshTokenRepository.RevokeAsync(tokenHash, cancellationToken: cancellationToken))
        {
            var user = await repository.GetByIdAsync(storedToken.UserRefId, cancellationToken);
            if (user is not null)
            {
                await LogAsync(eventLogger, user, EventAction.LoggedOut, cancellationToken);
            }
        }

        return Results.Ok(new { message = "Logged out." });
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IUserRepository repository,
        CancellationToken cancellationToken)
    {
        var id = principal.FindFirstValue(A360ClaimTypes.Subject);
        var user = id is null ? null : await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToAuthUser(user));
    }

    private static async Task<AuthTokenResponse> IssueTokensAsync(
        UserEntity user,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        string message,
        CancellationToken cancellationToken,
        IssuedRefreshToken? refreshToken = null)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        refreshToken ??= tokenService.CreateRefreshToken();

        await refreshTokenRepository.CreateAsync(new RefreshTokenEntity
        {
            UserRefId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt,
            ClientId = user.ClientId,
            TenantId = user.TenantId,
            CreatedBy = user.UserId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return new AuthTokenResponse(
            message,
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt,
            "Bearer",
            ToAuthUser(user));
    }

    private static async Task<UserEntity?> FindUniqueUserAsync(
        IUserRepository repository,
        string userName,
        CancellationToken cancellationToken)
    {
        // User names are not unique in the collection; refuse to guess which account is meant.
        var users = await repository.GetByUserNameAsync(userName.Trim(), limit: 2, cancellationToken);
        return users.Count == 1 ? users.First() : null;
    }

    private static Task LogAsync(IEventLogger eventLogger, UserEntity user, string action, CancellationToken cancellationToken)
    {
        return eventLogger.LogAsync(EventEntityType, user.Id, user.UserName, action, ToActor(user), cancellationToken);
    }

    // For attempts that cannot be tied to an account (unknown username or token): records what was tried.
    private static Task LogAttemptAsync(IEventLogger eventLogger, string? attemptedUserName, string action, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(attemptedUserName) ? "Unknown user" : attemptedUserName.Trim();
        if (name.Length > MaxLoggedUserNameLength)
        {
            name = name[..MaxLoggedUserNameLength];
        }

        return eventLogger.LogAsync(EventEntityType, string.Empty, name, action, new EventActor(null, name), cancellationToken);
    }

    private static Task LogTokenOwnerAsync(
        IEventLogger eventLogger,
        UserEntity? user,
        string userRefId,
        string action,
        CancellationToken cancellationToken)
    {
        var actor = user is null ? new EventActor(userRefId, "Unknown user") : ToActor(user);
        return eventLogger.LogAsync(EventEntityType, userRefId, actor.UserName, action, actor, cancellationToken);
    }

    // Auth events happen before (or without) a bearer token, so the account itself is recorded as the actor.
    private static EventActor ToActor(UserEntity user)
    {
        return new EventActor(user.Id, user.UserName, user.ClientId, user.TenantId);
    }

    private static bool CanSignIn(UserEntity user)
    {
        return !user.IsDeleted
            && !string.Equals(user.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(user.LoginStatus, "Inactive", StringComparison.OrdinalIgnoreCase);
    }

    private static AuthUserResponse ToAuthUser(UserEntity user)
    {
        return new AuthUserResponse(
            user.Id,
            user.UserId,
            user.UserName,
            user.Email,
            user.RoleName,
            user.UserRoleId,
            user.ClientId,
            user.TenantId);
    }

    private static IResult Unauthorized(string message)
    {
        return Results.Json(new { message }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        var name = email[..at];
        var visible = name.Length <= 2 ? name[..1] : name[..2];
        return $"{visible}***{email[at..]}";
    }

    private static string BuildOtpEmailBody(string userName, string code)
    {
        var minutes = (int)OneTimeCodeService.CodeLifetime.TotalMinutes;
        return $"""
            <p>Hello {WebUtility.HtmlEncode(userName)},</p>
            <p>Your Asset360 login verification code is:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">{code}</p>
            <p>This code expires in {minutes} minutes. If you did not try to sign in, please change your password.</p>
            """;
    }
}
