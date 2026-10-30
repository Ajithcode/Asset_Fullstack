using SystemUserEntity = A360.MasterDataManagement.Domain.Entities.SystemUser;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateSystemUserRequest(
    string? Code,
    string? FullName,
    string? Username,
    string? Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    List<MasterDataReferenceDto>? AdditionalRoles,
    string? Phone,
    string? PhotoUrl,
    string? EmployeeId,
    List<string>? Sites,
    DateTime? AccountExpiry,
    bool? MfaRequired,
    bool? MustChangePassword,
    bool? SendInvite,
    string? Language,
    string? TimeZone,
    string? DateFormat,
    bool? EmailNotifications,
    bool? SmsNotifications,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SystemUserEntity ToEntity()
    {
        return new SystemUserEntity
        {
            Code = SystemUserFields.Clean(Code)?.ToUpperInvariant(),
            FullName = FullName!.Trim(),
            Username = Username!.Trim(),
            Email = Email!.Trim(),
            Department = Department?.ToReference(),
            PrimaryRole = PrimaryRole?.ToReference(),
            AdditionalRoles = AdditionalRoles?.Select(x => x.ToReference()).ToList() ?? [],
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(PhotoUrl) ? null : PhotoUrl.Trim(),
            EmployeeId = SystemUserFields.Clean(EmployeeId),
            Sites = SystemUserFields.CleanList(Sites),
            AccountExpiry = SystemUserFields.AsDate(AccountExpiry),
            MfaRequired = MfaRequired ?? false,
            MustChangePassword = MustChangePassword ?? true,
            SendInvite = SendInvite ?? false,
            Language = SystemUserFields.Clean(Language),
            TimeZone = SystemUserFields.Clean(TimeZone),
            DateFormat = SystemUserFields.Clean(DateFormat),
            EmailNotifications = EmailNotifications ?? true,
            SmsNotifications = SmsNotifications ?? false,

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
    string? Code,
    string? FullName,
    string? Username,
    string? Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    List<MasterDataReferenceDto>? AdditionalRoles,
    string? Phone,
    string? PhotoUrl,
    string? EmployeeId,
    List<string>? Sites,
    DateTime? AccountExpiry,
    bool? MfaRequired,
    bool? MustChangePassword,
    bool? SendInvite,
    string? Language,
    string? TimeZone,
    string? DateFormat,
    bool? EmailNotifications,
    bool? SmsNotifications,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SystemUserEntity user)
    {
        if (!string.IsNullOrWhiteSpace(Code))
        {
            user.Code = Code.Trim().ToUpperInvariant();
        }

        user.FullName = FullName!.Trim();
        user.Username = Username!.Trim();
        user.Email = Email!.Trim();
        user.Department = Department?.ToReference();
        user.PrimaryRole = PrimaryRole?.ToReference();
        user.AdditionalRoles = AdditionalRoles?.Select(x => x.ToReference()).ToList() ?? [];
        user.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        user.PhotoUrl = string.IsNullOrWhiteSpace(PhotoUrl) ? null : PhotoUrl.Trim();
        user.EmployeeId = SystemUserFields.Clean(EmployeeId);
        user.Sites = SystemUserFields.CleanList(Sites);
        user.AccountExpiry = SystemUserFields.AsDate(AccountExpiry);
        user.MfaRequired = MfaRequired ?? user.MfaRequired;
        user.MustChangePassword = MustChangePassword ?? user.MustChangePassword;
        user.SendInvite = SendInvite ?? user.SendInvite;
        user.Language = SystemUserFields.Clean(Language);
        user.TimeZone = SystemUserFields.Clean(TimeZone);
        user.DateFormat = SystemUserFields.Clean(DateFormat);
        user.EmailNotifications = EmailNotifications ?? user.EmailNotifications;
        user.SmsNotifications = SmsNotifications ?? user.SmsNotifications;

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
    string? Code,
    string FullName,
    string Username,
    string Email,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? PrimaryRole,
    IReadOnlyList<MasterDataReferenceDto> AdditionalRoles,
    string? Phone,
    string? PhotoUrl,
    string? EmployeeId,
    IReadOnlyList<string> Sites,
    DateTime? AccountExpiry,
    bool MfaRequired,
    bool MustChangePassword,
    bool SendInvite,
    string? Language,
    string? TimeZone,
    string? DateFormat,
    bool EmailNotifications,
    bool SmsNotifications,
    DateTime? LastLoginAt,
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
            user.Code,
            user.FullName ?? string.Empty,
            user.Username ?? string.Empty,
            user.Email ?? string.Empty,
            user.Department is null
                ? null
                : MasterDataReferenceDto.FromReference(user.Department),
            user.PrimaryRole is null
                ? null
                : MasterDataReferenceDto.FromReference(user.PrimaryRole),
            (user.AdditionalRoles ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            user.Phone,
            user.PhotoUrl,
            user.EmployeeId,
            user.Sites ?? [],
            user.AccountExpiry,
            user.MfaRequired,
            user.MustChangePassword,
            user.SendInvite,
            user.Language,
            user.TimeZone,
            user.DateFormat,
            user.EmailNotifications,
            user.SmsNotifications,
            user.LastLoginAt,
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

internal static class SystemUserFields
{
    public static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static List<string> CleanList(IEnumerable<string>? values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    // The expiry is a calendar date; keep it at midnight UTC so it doesn't shift when stored.
    public static DateTime? AsDate(DateTime? value)
    {
        return value is null
            ? null
            : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
    }
}
