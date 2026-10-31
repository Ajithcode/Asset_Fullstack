
using AlertMasterEntity = A360.Wip.Domain.Entities.AlertMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateAlertMasterRequest(
    string? AlertId,
    string? AssetId,
    string? AssetName,
    string? AlertType,
    string? TriggerCondition,
    string? NotificationChannel,
    string? RecipientRole,
    string? EscalationLevel,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public AlertMasterEntity ToEntity()
    {
        return new AlertMasterEntity
        {
            AlertId = AlertId,
            AssetId = AssetId,
            AssetName = AssetName,

            AlertType = AlertType,
            TriggerCondition = TriggerCondition,
            NotificationChannel = NotificationChannel,
            RecipientRole = RecipientRole,
            EscalationLevel = EscalationLevel,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAlertMasterRequest(
    string? AssetId,
    string? AssetName,
    string? AlertType,
    string? TriggerCondition,
    string? NotificationChannel,
    string? RecipientRole,
    string? EscalationLevel,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(AlertMasterEntity alertMaster)
    {
        alertMaster.AssetId = AssetId;
        alertMaster.AssetName = AssetName;

        alertMaster.AlertType = AlertType;
        alertMaster.TriggerCondition = TriggerCondition;
        alertMaster.NotificationChannel = NotificationChannel;
        alertMaster.RecipientRole = RecipientRole;
        alertMaster.EscalationLevel = EscalationLevel;

        alertMaster.UpdatedBy = UpdatedBy;
        alertMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            alertMaster.Status = Status;
        }
    }
}

public sealed record AlertMasterResponse(
    string Id,
    string AlertId,
    string? AssetId,
    string? AssetName,
    string? AlertType,
    string? TriggerCondition,
    string? NotificationChannel,
    string? RecipientRole,
    string? EscalationLevel,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AlertMasterResponse FromEntity(AlertMasterEntity alertMaster)
    {
        return new AlertMasterResponse(
            alertMaster.Id ?? string.Empty,
            alertMaster.AlertId ?? string.Empty,
            alertMaster.AssetId,
            alertMaster.AssetName,

            alertMaster.AlertType,
            alertMaster.TriggerCondition,
            alertMaster.NotificationChannel,
            alertMaster.RecipientRole,
            alertMaster.EscalationLevel,

            alertMaster.CreatedBy,
            alertMaster.CreatedAt,
            alertMaster.UpdatedBy,
            alertMaster.UpdatedAt,

            alertMaster.ClientId,
            alertMaster.TenantId,
            alertMaster.Status,
            alertMaster.IsDeleted
        );
    }
}
