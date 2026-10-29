using SystemUserEntity = A360.MasterDataManagement.Domain.Entities.SystemUser;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateSystemUserRequest(
    string? FullName,
    string? Username,
    string? Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    string? Phone,
    string? PhotoUrl,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SystemUserEntity ToEntity()
    {
        return new SystemUserEntity
        {
            FullName = FullName!.Trim(),
            Username = Username!.Trim(),
            Email = Email!.Trim(),
            Department = Department?.ToReference(),
            PrimaryRole = PrimaryRole?.ToReference(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(PhotoUrl) ? null : PhotoUrl.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSystemUserRequest(
    string? FullName,
    string? Username,
    string? Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    string? Phone,
    string? PhotoUrl,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SystemUserEntity user)
    {
        user.FullName = FullName!.Trim();
        user.Username = Username!.Trim();
        user.Email = Email!.Trim();
        user.Department = Department?.ToReference();
        user.PrimaryRole = PrimaryRole?.ToReference();
        user.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        user.PhotoUrl = string.IsNullOrWhiteSpace(PhotoUrl) ? null : PhotoUrl.Trim();

        user.UpdatedBy = UpdatedBy;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            user.Status = Status;
        }
    }
}

public sealed record UpdateSystemUserStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record SystemUserResponse(
    string Id,
    string FullName,
    string Username,
    string Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    string? Phone,
    string? PhotoUrl,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SystemUserResponse FromEntity(SystemUserEntity user)
    {
        return new SystemUserResponse(
            user.Id ?? string.Empty,
            user.FullName ?? string.Empty,
            user.Username ?? string.Empty,
            user.Email ?? string.Empty,
            user.Department is null
                ? null
                : MasterDataReferenceDto.FromReference(user.Department),
            user.PrimaryRole is null
                ? null
                : MasterDataReferenceDto.FromReference(user.PrimaryRole),
            user.Phone,
            user.PhotoUrl,
            user.CreatedBy,
            user.CreatedAt,
            user.UpdatedBy,
            user.UpdatedAt,
            user.ClientId,
            user.TenantId,
            user.Status,
            user.IsDeleted
        );
    }
}
