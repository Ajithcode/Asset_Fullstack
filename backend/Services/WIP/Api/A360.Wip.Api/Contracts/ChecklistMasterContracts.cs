
using ChecklistMasterEntity = A360.Wip.Domain.Entities.ChecklistMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateChecklistMasterRequest(
    string? ChecklistId,
    string? AssetId,
    string? AssetName,
    string? ChecklistName,
    string? ChecklistType,
    string? ApplicableWorkType,
    int? VersionNumber,
    bool? IsMandatory,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public ChecklistMasterEntity ToEntity()
    {
        return new ChecklistMasterEntity
        {
            ChecklistId = ChecklistId,
            AssetId = AssetId,
            AssetName = AssetName,

            ChecklistName = ChecklistName,
            ChecklistType = ChecklistType,
            ApplicableWorkType = ApplicableWorkType,
            VersionNumber = VersionNumber,
            IsMandatory = IsMandatory ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateChecklistMasterRequest(
    string? AssetId,
    string? AssetName,
    string? ChecklistName,
    string? ChecklistType,
    string? ApplicableWorkType,
    int? VersionNumber,
    bool? IsMandatory,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(ChecklistMasterEntity checklistMaster)
    {
        checklistMaster.AssetId = AssetId;
        checklistMaster.AssetName = AssetName;

        checklistMaster.ChecklistName = ChecklistName;
        checklistMaster.ChecklistType = ChecklistType;
        checklistMaster.ApplicableWorkType = ApplicableWorkType;
        checklistMaster.VersionNumber = VersionNumber;
        checklistMaster.IsMandatory = IsMandatory ?? checklistMaster.IsMandatory;

        checklistMaster.UpdatedBy = UpdatedBy;
        checklistMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            checklistMaster.Status = Status;
        }
    }
}

public sealed record ChecklistMasterResponse(
    string Id,
    string ChecklistId,
    string? AssetId,
    string? AssetName,
    string ChecklistName,
    string? ChecklistType,
    string? ApplicableWorkType,
    int? VersionNumber,
    bool IsMandatory,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ChecklistMasterResponse FromEntity(ChecklistMasterEntity checklistMaster)
    {
        return new ChecklistMasterResponse(
            checklistMaster.Id ?? string.Empty,
            checklistMaster.ChecklistId ?? string.Empty,
            checklistMaster.AssetId,
            checklistMaster.AssetName,

            checklistMaster.ChecklistName ?? string.Empty,
            checklistMaster.ChecklistType,
            checklistMaster.ApplicableWorkType,
            checklistMaster.VersionNumber,
            checklistMaster.IsMandatory,

            checklistMaster.CreatedBy,
            checklistMaster.CreatedAt,
            checklistMaster.UpdatedBy,
            checklistMaster.UpdatedAt,

            checklistMaster.ClientId,
            checklistMaster.TenantId,
            checklistMaster.Status,
            checklistMaster.IsDeleted
        );
    }
}
