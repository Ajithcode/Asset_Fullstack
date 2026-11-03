using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using A360.Domain.Entities;
using A360.Email;
using A360.Repository.Activity;
using A360.Security;
using A360.UserAccount.Api.Contracts;
using A360.UserAccount.Api.Email;
using A360.UserAccount.Api.Security;
using A360.UserAccount.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Api.Endpoints;

public static class AuthEndpoints
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";
    private const string InvalidOtpMessage = "Invalid or expired OTP.";
    private const string InvalidRefreshTokenMessage = "Invalid or expired refresh token.";
    private const string InvalidTotpMessage = "Invalid authenticator code. Check that your phone's time is correct and try again.";
    private const string LoginExpiredMessage = "Your sign-in has expired. Please sign in again.";
    private const string TooManyAttemptsMessage = "Too many invalid attempts. Please log in again.";
    private const string EventEntityType = "User";
    private const int MaxLoggedUserNameLength = 100;

    // Verified against when the username is unknown, so both failure paths take the same time.
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHashingService().Hash(Guid.NewGuid().ToString()));

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/roles", GetSignupRolesAsync).WithName("GetSignupRoles").AllowAnonymous();
        group.MapPost("/register", RegisterAsync).WithName("Register").AllowAnonymous();
        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous();
        group.MapPost("/verify-otp", VerifyOtpAsync).WithName("VerifyOtp").AllowAnonymous();
        group.MapPost("/verify-totp", VerifyTotpAsync).WithName("VerifyTotp").AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).WithName("RefreshToken").AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).WithName("Logout").AllowAnonymous();
        group.MapGet("/me", GetCurrentUserAsync).WithName("GetCurrentUser").RequireAuthorization();

        return group;
    }

    /// <summary>
    /// Public self sign-up. The caller picks one of the roles offered by GET /api/auth/roles; the server
    /// checks it exists and is offered, and takes client/tenant from that role. Afterwards the user signs in (password + OTP).
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IUserRepository repository,
        IRoleRepository roleRepository,
        PasswordHashingService passwordHashingService,
        IConfiguration configuration,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var roleId = request.UserRoleId?.Trim() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["FullName"] = ["Full name is required."];
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(userName, "^[A-Za-z0-9._-]{3,50}$"))
        {
            errors["UserName"] = ["Username must be 3-50 characters: letters, numbers, dot, dash or underscore."];
        }
        if (!IsValidEmail(email))
        {
            errors["Email"] = ["Enter a valid email address."];
        }
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
        {
            errors["Password"] = ["Password must be at least 8 characters."];
        }
        if (string.IsNullOrEmpty(roleId))
        {
            errors["UserRoleId"] = ["Select a role."];
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Only a role that the sign-up page offers may be chosen; anything else is rejected, not silently replaced.
        var role = (await LoadSignupRolesAsync(roleRepository, configuration, cancellationToken))
            .FirstOrDefault(r => string.Equals(r.RoleId, roleId, StringComparison.Ordinal));
        if (role is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["UserRoleId"] = ["The selected role is not available."] });
        }

        // Login looks users up by user name and refuses ambiguous matches, so names must stay unique.
        if ((await repository.GetByUserNameAsync(userName, limit: 1, cancellationToken)).Count > 0)
        {
            return Results.Conflict(new { message = "That username is already taken." });
        }
        if (await repository.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }

        var settings = configuration.GetSection("Registration");
        var createRequest = new CreateUserRequest(
            UserName: userName,
            ShortName: fullName,
            ContactNo: request.ContactNo?.Trim() ?? string.Empty,
            Email: email,
            LoginPassword: request.Password,
            ActiveDirectoryUserName: string.Empty,
            UserRoleId: role.RoleId,
            CreatedBy: "self-registration",
            ClientId: string.IsNullOrWhiteSpace(role.ClientId) ? settings["ClientId"] ?? "CLT-1001" : role.ClientId,
            TenantId: string.IsNullOrWhiteSpace(role.TenantId) ? settings["TenantId"] ?? "TENANT-DEFAULT" : role.TenantId);

        var entity = createRequest.ToEntity(passwordHashingService);
        entity.RoleName = role.RoleName;
        var user = await repository.CreateAsync(entity, cancellationToken);
        await eventLogger.LogAsync(EventEntityType, user.Id, user.UserName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/users/{user.Id}", new RegisterResponse("Account created. You can now sign in.", user.UserName, user.Email));
    }

    /// <summary>Roles for the sign-up dropdown: id, name and description only, no permissions.</summary>
    private static async Task<IResult> GetSignupRolesAsync(
        IRoleRepository roleRepository,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var roles = await LoadSignupRolesAsync(roleRepository, configuration, cancellationToken);
        return Results.Ok(roles.Select(r => new SignupRoleResponse(r.RoleId, r.RoleName, r.Description)));
    }

    /// <summary>
    /// Active roles offered at sign-up. Registration:AllowedRoleNames limits them (empty = every active role),
    /// so privileged roles such as Admin can be kept out of public sign-up.
    /// </summary>
    private static async Task<IReadOnlyList<RoleEntity>> LoadSignupRolesAsync(
        IRoleRepository roleRepository,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var allowed = configuration.GetSection("Registration:AllowedRoleNames").Get<string[]>() ?? [];
        var roles = await roleRepository.GetAllAsync(cancellationToken);
        return roles
            .Where(r => !r.IsDeleted
                && !string.Equals(r.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(r.RoleId)
                && (allowed.Length == 0 || allowed.Contains(r.RoleName, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(r => r.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(email)
                && string.Equals(new System.Net.Mail.MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserRepository repository,
        PasswordHashingService passwordHashingService,
        OneTimeCodeService oneTimeCodeService,
        TotpService totpService,
        IConfiguration configuration,
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

        if (UsesAuthenticatorApp(configuration))
        {
            return await StartAuthenticatorLoginAsync(user, repository, oneTimeCodeService, totpService, eventLogger, cancellationToken);
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
            Method: "email",
            Message: "An OTP has been sent to your registered email address.",
            OtpExpiresInSeconds: (int)OneTimeCodeService.CodeLifetime.TotalSeconds,
            MaskedEmail: MaskEmail(user.Email)));
    }

    /// <summary>TwoFactor:Method decides the second step: "Totp" (authenticator app, the default) or "Email".</summary>
    private static bool UsesAuthenticatorApp(IConfiguration configuration)
    {
        return !string.Equals(configuration["TwoFactor:Method"], "Email", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Password accepted, authenticator app next. A random login token (stored hashed, single use, 5 minutes) ties
    /// the code step to this password check. Users without the app set up get a QR code to scan first; the
    /// secret stays pending until they confirm a code from it, and is reused if they sign in again meanwhile.
    /// </summary>
    private static async Task<IResult> StartAuthenticatorLoginAsync(
        UserEntity user,
        IUserRepository repository,
        OneTimeCodeService oneTimeCodeService,
        TotpService totpService,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var loginToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiration = DateTime.UtcNow.Add(OneTimeCodeService.CodeLifetime);
        await repository.SetTwoFactorCodeAsync(user.Id, oneTimeCodeService.Hash(loginToken), expiration, cancellationToken);
        var expiresIn = (int)OneTimeCodeService.CodeLifetime.TotalSeconds;

        if (user.TotpEnabled && totpService.Unprotect(user.TotpSecret) is not null)
        {
            return Results.Ok(new LoginResponse(
                TwoFactorRequired: true,
                Method: "totp",
                Message: "Enter the 6-digit code from your authenticator app.",
                OtpExpiresInSeconds: expiresIn,
                LoginToken: loginToken));
        }

        var secret = totpService.Unprotect(user.TotpPendingSecret);
        if (secret is null)
        {
            secret = totpService.GenerateSecret();
            await repository.SetPendingTotpSecretAsync(user.Id, totpService.Protect(secret), cancellationToken);
        }

        await LogAsync(eventLogger, user, EventAction.TotpSetupStarted, cancellationToken);
        return Results.Ok(new LoginResponse(
            TwoFactorRequired: true,
            Method: "totp",
            Message: "Scan the QR code with Google Authenticator, then enter the 6-digit code it shows.",
            OtpExpiresInSeconds: expiresIn,
            LoginToken: loginToken,
            SetupRequired: true,
            QrCodeImage: TotpService.BuildQrCodeDataUri(totpService.BuildOtpAuthUri(user.UserName, secret)),
            ManualEntryKey: TotpService.FormatForManualEntry(secret)));
    }

    /// <summary>
    /// Second step with the authenticator app: checks the login token from /login and the 6-digit code, then issues
    /// tokens. During setup the first valid code also turns the authenticator on for the account.
    /// </summary>
    private static async Task<IResult> VerifyTotpAsync(
        VerifyTotpRequest request,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        OneTimeCodeService oneTimeCodeService,
        TotpService totpService,
        TokenService tokenService,
        IConfiguration configuration,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        // With emailed codes, two_factor_code holds the emailed code, not a login token.
        if (!UsesAuthenticatorApp(configuration))
        {
            return Results.BadRequest(new { message = "Sign-in uses emailed codes. Use POST /api/auth/verify-otp." });
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.LoginToken) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest(new { message = "Username, login token and code are required." });
        }

        var user = await FindUniqueUserAsync(repository, request.UserName, cancellationToken);
        if (user is null)
        {
            await LogAttemptAsync(eventLogger, request.UserName, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(LoginExpiredMessage);
        }

        if (user.TwoFactorExpiration is null
            || user.TwoFactorExpiration <= DateTime.UtcNow
            || !oneTimeCodeService.Matches(request.LoginToken, user.TwoFactorCode))
        {
            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(LoginExpiredMessage);
        }

        if (user.TwoFactorAttempts >= OneTimeCodeService.MaxAttempts)
        {
            await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
            await LogAsync(eventLogger, user, EventAction.OtpLocked, cancellationToken);
            return Unauthorized(TooManyAttemptsMessage);
        }

        var settingUp = !user.TotpEnabled;
        var protectedSecret = settingUp ? user.TotpPendingSecret : user.TotpSecret;
        var secret = totpService.Unprotect(protectedSecret);
        if (secret is null)
        {
            await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
            return Unauthorized(LoginExpiredMessage);
        }

        var step = totpService.Verify(secret, request.Code, settingUp ? 0 : user.TotpLastUsedStep);
        if (step is null)
        {
            var attempts = await repository.RegisterFailedTwoFactorAttemptAsync(user.Id, cancellationToken);
            if (attempts >= OneTimeCodeService.MaxAttempts)
            {
                await repository.ClearTwoFactorCodeAsync(user.Id, cancellationToken);
                await LogAsync(eventLogger, user, EventAction.OtpLocked, cancellationToken);
                return Unauthorized(TooManyAttemptsMessage);
            }

            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidTotpMessage);
        }

        if (!CanSignIn(user))
        {
            await LogAsync(eventLogger, user, EventAction.LoginBlocked, cancellationToken);
            return Unauthorized(LoginExpiredMessage);
        }

        var stepAccepted = settingUp
            ? await repository.EnableTotpAsync(user.Id, protectedSecret, step.Value, cancellationToken)
            : await repository.TryUseTotpStepAsync(user.Id, step.Value, cancellationToken);
        if (!stepAccepted)
        {
            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(InvalidTotpMessage);
        }

        if (settingUp)
        {
            await LogAsync(eventLogger, user, EventAction.TotpEnabled, cancellationToken);
        }

        // Matching on the stored login token makes it single-use.
        if (!await repository.CompleteTwoFactorLoginAsync(user.Id, user.TwoFactorCode, cancellationToken))
        {
            await LogAsync(eventLogger, user, EventAction.OtpFailed, cancellationToken);
            return Unauthorized(LoginExpiredMessage);
        }

        var response = await IssueTokensAsync(user, refreshTokenRepository, tokenService, "Login successful.", cancellationToken);
        await LogAsync(eventLogger, user, EventAction.LoggedIn, cancellationToken);
        return Results.Ok(response);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static async Task<IResult> VerifyOtpAsync(
        VerifyOtpRequest request,
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        OneTimeCodeService oneTimeCodeService,
        TokenService tokenService,
        IConfiguration configuration,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        // With the authenticator app, two_factor_code holds the login token, not an emailed code;
        // accepting it here would let the token alone skip the authenticator step.
        if (UsesAuthenticatorApp(configuration))
        {
            return Results.BadRequest(new { message = "Sign-in uses the authenticator app. Use POST /api/auth/verify-totp." });
        }

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
