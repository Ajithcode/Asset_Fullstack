using PermitTypeEntity = A360.MasterDataManagement.Domain.Entities.PermitType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreatePermitTypeRequest(
    string? PermitTypeName,
    string? Code,
    string? Category,
    int? ValidTillDays,
    string? Description,
    int? ApprovalLevels,
    List<string>? ApproverRoles,
    int? MaxExtensionDays,
    bool? IsolationRequired,
    bool? GasTestRequired,
    bool? CloseOutInspection,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public PermitTypeEntity ToEntity()
    {
        return new PermitTypeEntity
        {
            PermitTypeName = PermitTypeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            ValidTillDays = ValidTillDays,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ApprovalLevels = ApprovalLevels,
            ApproverRoles = ApproverRoles?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            MaxExtensionDays = MaxExtensionDays,
            IsolationRequired = IsolationRequired ?? false,
            GasTestRequired = GasTestRequired ?? false,
            CloseOutInspection = CloseOutInspection ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePermitTypeRequest(
    string? PermitTypeName,
    string? Code,
    string? Category,
    int? ValidTillDays,
    string? Description,
    int? ApprovalLevels,
    List<string>? ApproverRoles,
    int? MaxExtensionDays,
    bool? IsolationRequired,
    bool? GasTestRequired,
    bool? CloseOutInspection,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(PermitTypeEntity permitType)
    {
        permitType.PermitTypeName = PermitTypeName!.Trim();
        permitType.Code = Code!.Trim();
        permitType.Category = Category!.Trim();
        permitType.ValidTillDays = ValidTillDays;
        permitType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        permitType.ApprovalLevels = ApprovalLevels;
        permitType.ApproverRoles = ApproverRoles?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        permitType.MaxExtensionDays = MaxExtensionDays;
        permitType.IsolationRequired = IsolationRequired ?? false;
        permitType.GasTestRequired = GasTestRequired ?? false;
        permitType.CloseOutInspection = CloseOutInspection ?? false;

        permitType.UpdatedBy = UpdatedBy;
        permitType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            permitType.Status = Status;
        }
    }
}

public sealed record UpdatePermitTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record PermitTypeResponse(
    string Id,
    string PermitTypeName,
    string Code,
    string Category,
    int? ValidTillDays,
    string? Description,
    int? ApprovalLevels,
    IReadOnlyList<string> ApproverRoles,
    int? MaxExtensionDays,
    bool IsolationRequired,
    bool GasTestRequired,
    bool CloseOutInspection,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PermitTypeResponse FromEntity(PermitTypeEntity permitType)
    {
        return new PermitTypeResponse(
            permitType.Id ?? string.Empty,
            permitType.PermitTypeName ?? string.Empty,
            permitType.Code ?? string.Empty,
            permitType.Category ?? string.Empty,
            permitType.ValidTillDays,
            permitType.Description,
            permitType.ApprovalLevels,
            permitType.ApproverRoles ?? [],
            permitType.MaxExtensionDays,
            permitType.IsolationRequired,
            permitType.GasTestRequired,
            permitType.CloseOutInspection,
            permitType.CreatedBy,
            permitType.CreatedAt,
            permitType.UpdatedBy,
            permitType.UpdatedAt,
            permitType.ClientId,
            permitType.TenantId,
            permitType.Status,
            permitType.IsDeleted
        );
    }
}
