
using PermitMasterEntity = A360.Wip.Domain.Entities.PermitMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreatePermitMasterRequest(
    string? PermitId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? PermitType,
    string? IssuedBy,
    string? ApprovedBy,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? DocumentAttachment,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public PermitMasterEntity ToEntity()
    {
        return new PermitMasterEntity
        {
            PermitId = PermitId,
            AssetId = AssetId,
            AssetName = AssetName,
            JobId = JobId,

            PermitType = PermitType,
            IssuedBy = IssuedBy,
            ApprovedBy = ApprovedBy,

            ValidFrom = ValidFrom,
            ValidTo = ValidTo,
            DocumentAttachment = DocumentAttachment,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePermitMasterRequest(
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? PermitType,
    string? IssuedBy,
    string? ApprovedBy,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? DocumentAttachment,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(PermitMasterEntity permitMaster)
    {
        permitMaster.AssetId = AssetId;
        permitMaster.AssetName = AssetName;
        permitMaster.JobId = JobId;

        permitMaster.PermitType = PermitType;
        permitMaster.IssuedBy = IssuedBy;
        permitMaster.ApprovedBy = ApprovedBy;

        permitMaster.ValidFrom = ValidFrom;
        permitMaster.ValidTo = ValidTo;
        permitMaster.DocumentAttachment = DocumentAttachment;

        permitMaster.UpdatedBy = UpdatedBy;
        permitMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            permitMaster.Status = Status;
        }
    }
}

public sealed record PermitMasterResponse(
    string Id,
    string PermitId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? PermitType,
    string? IssuedBy,
    string? ApprovedBy,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? DocumentAttachment,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PermitMasterResponse FromEntity(PermitMasterEntity permitMaster)
    {
        return new PermitMasterResponse(
            permitMaster.Id ?? string.Empty,
            permitMaster.PermitId ?? string.Empty,
            permitMaster.AssetId,
            permitMaster.AssetName,
            permitMaster.JobId,

            permitMaster.PermitType,
            permitMaster.IssuedBy,
            permitMaster.ApprovedBy,

            permitMaster.ValidFrom,
            permitMaster.ValidTo,
            permitMaster.DocumentAttachment,

            permitMaster.CreatedBy,
            permitMaster.CreatedAt,
            permitMaster.UpdatedBy,
            permitMaster.UpdatedAt,

            permitMaster.ClientId,
            permitMaster.TenantId,
            permitMaster.Status,
            permitMaster.IsDeleted
        );
    }
}
