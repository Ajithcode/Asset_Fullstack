namespace A360.UserAccount.Api.Contracts;

public sealed record LoginRequest(string? UserName, string? Password);

/// <summary>Public self sign-up. UserRoleId must be one of the roles from GET /api/auth/roles; client and tenant come from that role.</summary>
public sealed record RegisterRequest(
    string? FullName,
    string? UserName,
    string? Email,
    string? ContactNo,
    string? Password,
    string? UserRoleId);

public sealed record RegisterResponse(string Message, string UserName, string Email);

/// <summary>A role offered on the sign-up page (no permissions exposed).</summary>
public sealed record SignupRoleResponse(string RoleId, string RoleName, string Description);

/// <summary>
/// Password accepted; a second factor is needed. Method "email": a code was emailed (POST /api/auth/verify-otp).
/// Method "totp": a code from the authenticator app (POST /api/auth/verify-totp with LoginToken); when SetupRequired,
/// the app is not set up yet and QrCodeImage / ManualEntryKey are filled in for the user to scan first.
/// </summary>
public sealed record LoginResponse(
    bool TwoFactorRequired,
    string Method,
    string Message,
    int OtpExpiresInSeconds,
    string? MaskedEmail = null,
    string? LoginToken = null,
    bool SetupRequired = false,
    string? QrCodeImage = null,
    string? ManualEntryKey = null);

public sealed record VerifyOtpRequest(string? UserName, string? Otp);

/// <summary>Second sign-in step with an authenticator app. LoginToken comes from the login response.</summary>
public sealed record VerifyTotpRequest(string? UserName, string? LoginToken, string? Code);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record AuthUserResponse(
    string Id,
    string UserId,
    string UserName,
    string Email,
    string RoleName,
    string UserRoleId,
    string? ClientId,
    string? TenantId);

public sealed record AuthTokenResponse(
    string Message,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string TokenType,
    AuthUserResponse User);
