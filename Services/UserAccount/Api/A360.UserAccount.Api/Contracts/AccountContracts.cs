namespace A360.UserAccount.Api.Contracts;

/// <summary>
/// POST /api/account/change-password. RefreshToken is the caller's own session token (optional): that session stays
/// signed in, every other one is signed out.
/// </summary>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? RefreshToken);

public sealed record ChangePasswordResponse(string Message, long OtherSessionsEnded);

/// <summary>
/// PUT /api/account/users/{id}/password (administrator). With NewPassword that password is set; without it a temporary
/// password is generated and returned once.
/// </summary>
public sealed record ResetPasswordRequest(string? NewPassword);

public sealed record ResetPasswordResponse(string Message, string? TemporaryPassword, long SessionsEnded);
