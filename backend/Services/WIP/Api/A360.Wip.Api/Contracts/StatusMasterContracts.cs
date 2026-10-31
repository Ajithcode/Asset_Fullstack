
using StatusMasterEntity = A360.Wip.Domain.Entities.StatusMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateStatusMasterRequest(
    string? StatusId,
    string? AssetId,
    string? AssetName,
    string? StatusName,
    string? StatusCode,
    int? SequenceOrder,
    bool IsClosedStatus,
    string? ColorCode,
    List<string>? AllowedTransitions,
    bool RequiresApproval,
    bool IsDefault,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public StatusMasterEntity ToEntity()
    {
        return new StatusMasterEntity
        {
            StatusId = StatusId,
            AssetId = AssetId,
            AssetName = AssetName,

            StatusName = StatusName,
            StatusCode = StatusCode,
            SequenceOrder = SequenceOrder,

            IsClosedStatus = IsClosedStatus,
            ColorCode = ColorCode,
            AllowedTransitions = AllowedTransitions ?? [],

            RequiresApproval = RequiresApproval,
            IsDefault = IsDefault,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateStatusMasterRequest(
    string? AssetId,
    string? AssetName,
    string? StatusName,
    string? StatusCode,
    int? SequenceOrder,
    bool IsClosedStatus,
    string? ColorCode,
    List<string>? AllowedTransitions,
    bool RequiresApproval,
    bool IsDefault,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(StatusMasterEntity statusMaster)
    {
        statusMaster.AssetId = AssetId;
        statusMaster.AssetName = AssetName;

        statusMaster.StatusName = StatusName;
        statusMaster.StatusCode = StatusCode;
        statusMaster.SequenceOrder = SequenceOrder;

        statusMaster.IsClosedStatus = IsClosedStatus;
        statusMaster.ColorCode = ColorCode;
        statusMaster.AllowedTransitions = AllowedTransitions ?? [];

        statusMaster.RequiresApproval = RequiresApproval;
        statusMaster.IsDefault = IsDefault;

        statusMaster.UpdatedBy = UpdatedBy;
        statusMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            statusMaster.Status = Status;
        }
    }
}

public sealed record StatusMasterResponse(
    string Id,
    string StatusId,
    string AssetId,
    string? AssetName,
    string StatusName,
    string StatusCode,
    int? SequenceOrder,
    bool IsClosedStatus,
    string? ColorCode,
    List<string> AllowedTransitions,
    bool RequiresApproval,
    bool IsDefault,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static StatusMasterResponse FromEntity(StatusMasterEntity statusMaster)
    {
        return new StatusMasterResponse(
            statusMaster.Id ?? string.Empty,
            statusMaster.StatusId ?? string.Empty,
            statusMaster.AssetId ?? string.Empty,
            statusMaster.AssetName,

            statusMaster.StatusName ?? string.Empty,
            statusMaster.StatusCode ?? string.Empty,
            statusMaster.SequenceOrder,

            statusMaster.IsClosedStatus,
            statusMaster.ColorCode,
            statusMaster.AllowedTransitions,

            statusMaster.RequiresApproval,
            statusMaster.IsDefault,

            statusMaster.CreatedBy,
            statusMaster.CreatedAt,
            statusMaster.UpdatedBy,
            statusMaster.UpdatedAt,

            statusMaster.ClientId,
            statusMaster.TenantId,
            statusMaster.Status,
            statusMaster.IsDeleted
        );
    }
}
