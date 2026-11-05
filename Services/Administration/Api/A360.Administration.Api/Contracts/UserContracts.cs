using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateUserRequest(
    string? Name,
    string? Username,
    string? Image,
    string? Email,
    string? EmployeeId,
    string? Department,
    string? JobTitle,
    string? Phone,
    string? Location,
    string? TimeZone,
    string? PrimaryRole,
    List<string>? SecondaryRoles,
    string? AccessScope,
    bool? MfaEnabled,
    List<string>? ModuleAccess,
    string? AccountStatus,
    string? CreatedBy,
    string? TenantId,
    string? ClientId,
    // Sign-in password; hashed by the UserAccount service and never returned
    string? Password);

// PUT /api/users/{userId}: same editable fields as create. A blank password keeps the current one.
public sealed record UpdateUserRequest(
    string? Name,
    string? Username,
    string? Image,
    string? Email,
    string? EmployeeId,
    string? Department,
    string? JobTitle,
    string? Phone,
    string? Location,
    string? TimeZone,
    string? PrimaryRole,
    List<string>? SecondaryRoles,
    string? AccountStatus,
    string? AccessScope,
    bool? MfaEnabled,
    List<string>? ModuleAccess,
    string? UpdatedBy,
    string? Password);

/// <summary>A module the signed-in user may open, with the actions their roles allow on it.</summary>
public sealed record ModuleAccessResponse(string Module, IReadOnlyCollection<string> Actions);

/// <summary>GET /api/users/me: who is signed in and what they may see.</summary>
public sealed record MyAccessResponse(
    string DisplayName,
    string Email,
    string RoleName,
    IReadOnlyCollection<string> Roles,
    // Sign-in administrators see everything; everyone else only the modules below
    bool FullAccess,
    IReadOnlyCollection<ModuleAccessResponse> Modules,
    UserResponse? User);

public sealed record UserResponse(
    string UserId,
    string Name,
    string Username,
    string? Image,
    string Email,
    string? EmployeeId,
    string? Department,
    string? JobTitle,
    string? Phone,
    string? Location,
    string? TimeZone,
    string PrimaryRole,
    IReadOnlyCollection<string> SecondaryRoles,
    string? AccessScope,
    bool MfaEnabled,
    IReadOnlyCollection<string> ModuleAccess,
    string? AccountStatus,
    DateTime? LastLoginAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId,
    // True when the user has a sign-in account (a password was set)
    bool HasLogin)
{
    public static UserResponse FromEntity(User user)
    {
        return new UserResponse(
            user.UserId,
            user.Name,
            user.Username,
            user.Image,
            user.Email,
            user.EmployeeId,
            user.Department,
            user.JobTitle,
            user.Phone,
            user.Location,
            user.TimeZone,
            user.PrimaryRole,
            user.SecondaryRoles,
            user.AccessScope,
            user.MfaEnabled,
            user.ModuleAccess,
            user.Status,
            user.LastLoginAt,
            user.CreatedBy,
            user.CreatedAt,
            user.UpdatedBy,
            user.UpdatedAt,
            user.TenantId,
            user.ClientId,
            !string.IsNullOrWhiteSpace(user.LoginAccountId));
    }
}
