using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Api.Contracts;

public sealed record CreateUserRequest(
    string? UserName,
    string? ShortName,
    string? ContactNo,
    string? Email,
    string? LoginPassword,
    string? ActiveDirectoryUserName,
    string? UserRoleId,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record UpdateUserRequest(
    string? UserName,
    string? ShortName,
    string? ContactNo,
    string? Email,
    string? LoginPassword,
    string? ActiveDirectoryUserName,
    string? UserRoleId,
    string? UpdatedBy,
    string? Status);

public sealed record UserResponse(
    string Id,
    string UserId,
    string UserName,
    string ShortName,
    string ContactNo,
    string Email,
    string ActiveDirectoryUserName,
    string UserRoleId,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted,
    DateTime? LastLogin,
    string LoginStatus)
{
    public static UserResponse FromEntity(UserEntity user)
    {
        return new UserResponse(
            user.Id,
            user.UserId,
            user.UserName,
            user.ShortName,
            user.ContactNo,
            user.Email,
            user.ActiveDirectoryUserName,
            user.UserRoleId,
            user.CreatedBy,
            user.CreatedAt,
            user.UpdatedBy,
            user.UpdatedAt,
            user.ClientId,
            user.TenantId,
            user.Status,
            user.IsDeleted,
            user.LastLogin,
            user.LoginStatus);
    }
}
