
using SlaMasterEntity = A360.Wip.Domain.Entities.SlaMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateSlaMasterRequest(
    string? SlaId,
    string? AssetId,
    string? AssetName,
    string? SlaName,
    string? WorkType,
    string? Priority,
    int? ResponseTimeMinutes,
    int? ResolutionTimeMinutes,
    string? EscalationLevel1,
    string? EscalationLevel2,
    string? EscalationLevel3,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public SlaMasterEntity ToEntity()
    {
        return new SlaMasterEntity
        {
            SlaId = SlaId,
            AssetId = AssetId,
            AssetName = AssetName,

            SlaName = SlaName,
            WorkType = WorkType,
            Priority = Priority,

            ResponseTimeMinutes = ResponseTimeMinutes,
            ResolutionTimeMinutes = ResolutionTimeMinutes,

            EscalationLevel1 = EscalationLevel1,
            EscalationLevel2 = EscalationLevel2,
            EscalationLevel3 = EscalationLevel3,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSlaMasterRequest(
    string? AssetId,
    string? AssetName,
    string? SlaName,
    string? WorkType,
    string? Priority,
    int? ResponseTimeMinutes,
    int? ResolutionTimeMinutes,
    string? EscalationLevel1,
    string? EscalationLevel2,
    string? EscalationLevel3,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(SlaMasterEntity slaMaster)
    {
        slaMaster.AssetId = AssetId;
        slaMaster.AssetName = AssetName;

        slaMaster.SlaName = SlaName;
        slaMaster.WorkType = WorkType;
        slaMaster.Priority = Priority;

        slaMaster.ResponseTimeMinutes = ResponseTimeMinutes;
        slaMaster.ResolutionTimeMinutes = ResolutionTimeMinutes;

        slaMaster.EscalationLevel1 = EscalationLevel1;
        slaMaster.EscalationLevel2 = EscalationLevel2;
        slaMaster.EscalationLevel3 = EscalationLevel3;

        slaMaster.UpdatedBy = UpdatedBy;
        slaMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            slaMaster.Status = Status;
        }
    }
}

public sealed record SlaMasterResponse(
    string Id,
    string SlaId,
    string? AssetId,
    string? AssetName,
    string SlaName,
    string? WorkType,
    string? Priority,
    int? ResponseTimeMinutes,
    int? ResolutionTimeMinutes,
    string? EscalationLevel1,
    string? EscalationLevel2,
    string? EscalationLevel3,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SlaMasterResponse FromEntity(SlaMasterEntity slaMaster)
    {
        return new SlaMasterResponse(
            slaMaster.Id ?? string.Empty,
            slaMaster.SlaId ?? string.Empty,
            slaMaster.AssetId,
            slaMaster.AssetName,

            slaMaster.SlaName ?? string.Empty,
            slaMaster.WorkType,
            slaMaster.Priority,

            slaMaster.ResponseTimeMinutes,
            slaMaster.ResolutionTimeMinutes,

            slaMaster.EscalationLevel1,
            slaMaster.EscalationLevel2,
            slaMaster.EscalationLevel3,

            slaMaster.CreatedBy,
            slaMaster.CreatedAt,
            slaMaster.UpdatedBy,
            slaMaster.UpdatedAt,

            slaMaster.ClientId,
            slaMaster.TenantId,
            slaMaster.Status,
            slaMaster.IsDeleted
        );
    }
}
