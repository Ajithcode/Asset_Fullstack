using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Raises an alert (a row in Recent Alerts &amp; Exceptions). The AlertId (e.g. AL000001) is generated.</summary>
/// <param name="RaisedAt">When the alert was raised; defaults to now.</param>
/// <param name="Severity">One of <see cref="IoTAlertSeverity.All"/>.</param>
/// <param name="Status">One of <see cref="IoTAlertStatus.All"/>; defaults to Open.</param>
public sealed record CreateIoTAlertRequest(
    DateTime? RaisedAt,
    string? AssetRegistrationId,
    string? AssetName,
    string? DeviceId,
    string? SiteId,
    string? SiteName,
    string? EventType,
    string? Severity,
    string? Status,
    string? Message,
    string? AlertRuleId,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public IoTAlert ToEntity(string alertId, CurrentUser? currentUser)
    {
        var alert = new IoTAlert
        {
            AlertId = alertId,
            RaisedAt = RaisedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetName = AssetName!.Trim(),
            DeviceId = ContractText.TrimOrNull(DeviceId),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            EventType = EventType!.Trim(),
            Severity = ContractText.Canonical(Severity, IoTAlertSeverity.All)!,
            Message = ContractText.TrimOrNull(Message),
            AlertRuleId = ContractText.TrimOrNull(AlertRuleId),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        alert.ApplyStatus(ContractText.Canonical(Status, IoTAlertStatus.All) ?? IoTAlertStatus.Open, currentUser);
        return alert;
    }
}

/// <summary>Moves an alert through Open → Acknowledged → In Progress → Resolved (any order is allowed, e.g. reopening).</summary>
/// <param name="Status">One of <see cref="IoTAlertStatus.All"/>.</param>
public sealed record UpdateIoTAlertStatusRequest(string? Status);

public static class IoTAlertStatusTransitions
{
    /// <summary>Sets the status and stamps who acknowledged / resolved it; reopening clears the resolution.</summary>
    public static void ApplyStatus(this IoTAlert alert, string status, CurrentUser? currentUser)
    {
        alert.Status = status;
        var now = DateTime.UtcNow;

        if (status is IoTAlertStatus.Acknowledged or IoTAlertStatus.InProgress or IoTAlertStatus.Resolved && alert.AcknowledgedAt is null)
        {
            alert.AcknowledgedAt = now;
            alert.AcknowledgedBy = currentUser?.UserName;
        }

        if (status == IoTAlertStatus.Resolved)
        {
            alert.ResolvedAt ??= now;
            alert.ResolvedBy ??= currentUser?.UserName;
        }
        else
        {
            alert.ResolvedAt = null;
            alert.ResolvedBy = null;
        }

        if (status == IoTAlertStatus.Open)
        {
            alert.AcknowledgedAt = null;
            alert.AcknowledgedBy = null;
        }
    }
}

public sealed record IoTAlertResponse(
    string Id,
    string AlertId,
    DateTime RaisedAt,
    string? AssetRegistrationId,
    string AssetName,
    string? DeviceId,
    string? SiteId,
    string? SiteName,
    string EventType,
    string Severity,
    string? Status,
    string? Message,
    string? AlertRuleId,
    string? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    string? ResolvedBy,
    DateTime? ResolvedAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static IoTAlertResponse FromEntity(IoTAlert alert)
    {
        return new IoTAlertResponse(
            alert.Id,
            alert.AlertId,
            alert.RaisedAt,
            alert.AssetRegistrationId,
            alert.AssetName,
            alert.DeviceId,
            alert.SiteId,
            alert.SiteName,
            alert.EventType,
            alert.Severity,
            alert.Status,
            alert.Message,
            alert.AlertRuleId,
            alert.AcknowledgedBy,
            alert.AcknowledgedAt,
            alert.ResolvedBy,
            alert.ResolvedAt,
            alert.CreatedBy,
            alert.CreatedAt,
            alert.UpdatedBy,
            alert.UpdatedAt,
            alert.ClientId,
            alert.TenantId);
    }
}

/// <summary>The Alerts KPI card: alerts raised in the last <see cref="Days"/> days, split by status and severity.</summary>
/// <param name="ChangePercent">Change of <see cref="Total"/> vs. the previous period of the same length; null when that had none.</param>
public sealed record IoTAlertCountResponse(
    int Days,
    DateTime From,
    DateTime To,
    int Total,
    int PreviousPeriodTotal,
    double? ChangePercent,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> BySeverity);

public sealed record IoTAlertOptionsResponse(
    IReadOnlyList<string> Severities,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> EventTypes)
{
    public static readonly IoTAlertOptionsResponse Instance =
        new(IoTAlertSeverity.All, IoTAlertStatus.All, IoTAlertEventType.Suggested);
}
