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
    string? ClientId);

// UserId, audit and tenant/client fields are fixed once the user is created.
// A blank Username keeps the existing one.
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
    string? AccessScope,
    bool? MfaEnabled,
    List<string>? ModuleAccess,
    string? AccountStatus);

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
    string? ClientId)
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
            user.ClientId);
    }
}
