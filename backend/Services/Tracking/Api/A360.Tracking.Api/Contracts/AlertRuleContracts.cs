using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Saves the Alert Rule Builder: Trigger, Severity, Applies to asset group, Condition, Actions, Notification channels.</summary>
/// <param name="Trigger">One of <see cref="AlertRuleTrigger.All"/>.</param>
/// <param name="Severity">One of <see cref="AlertRuleSeverity.All"/>.</param>
/// <param name="Condition">One of <see cref="AlertRuleCondition.All"/>; defaults to "During all hours".</param>
/// <param name="DwellMinutes">Required when Trigger is "When asset remains in zone".</param>
/// <param name="NotificationChannels">Each one of <see cref="AlertNotificationChannel.All"/>.</param>
/// <param name="Status">One of <see cref="AlertRuleStatus.All"/>; defaults to Active.</param>
public sealed record CreateAlertRuleRequest(
    string? Name,
    string? GeofenceId,
    string? Trigger,
    string? Severity,
    string? AssetGroup,
    string? Condition,
    int? DwellMinutes,
    bool? CreateAlert,
    bool? NotifyTeam,
    string? NotifyTeamName,
    bool? StartWorkflow,
    string? WorkflowName,
    IReadOnlyList<string>? NotificationChannels,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AlertRule ToEntity(Geofence geofence, CurrentUser? currentUser)
    {
        var notifyTeam = NotifyTeam ?? false;
        var startWorkflow = StartWorkflow ?? false;

        return new AlertRule
        {
            Name = Name!.Trim(),
            GeofenceId = geofence.Id,
            GeofenceName = geofence.Name,
            Trigger = ContractText.Canonical(Trigger, AlertRuleTrigger.All)!,
            Severity = ContractText.Canonical(Severity, AlertRuleSeverity.All)!,
            AssetGroup = AssetGroup!.Trim(),
            Condition = ContractText.Canonical(Condition, AlertRuleCondition.All) ?? AlertRuleCondition.AllHours,
            DwellMinutes = DwellMinutes,
            CreateAlert = CreateAlert ?? true,
            NotifyTeam = notifyTeam,
            NotifyTeamName = notifyTeam ? ContractText.TrimOrNull(NotifyTeamName) : null,
            StartWorkflow = startWorkflow,
            WorkflowName = startWorkflow ? ContractText.TrimOrNull(WorkflowName) : null,
            NotificationChannels = (NotificationChannels ?? [])
                .Select(channel => ContractText.Canonical(channel, AlertNotificationChannel.All)!)
                .Distinct()
                .ToList(),
            Status = ContractText.Canonical(Status, AlertRuleStatus.All) ?? AlertRuleStatus.Active,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record AlertRuleResponse(
    string Id,
    string Name,
    string GeofenceId,
    string GeofenceName,
    string Trigger,
    string Severity,
    string AssetGroup,
    string Condition,
    int? DwellMinutes,
    bool CreateAlert,
    bool NotifyTeam,
    string? NotifyTeamName,
    bool StartWorkflow,
    string? WorkflowName,
    IReadOnlyList<string> NotificationChannels,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AlertRuleResponse FromEntity(AlertRule rule)
    {
        return new AlertRuleResponse(
            rule.Id,
            rule.Name,
            rule.GeofenceId,
            rule.GeofenceName,
            rule.Trigger,
            rule.Severity,
            rule.AssetGroup,
            rule.Condition,
            rule.DwellMinutes,
            rule.CreateAlert,
            rule.NotifyTeam,
            rule.NotifyTeamName,
            rule.StartWorkflow,
            rule.WorkflowName,
            rule.NotificationChannels,
            rule.Status,
            rule.CreatedBy,
            rule.CreatedAt,
            rule.ClientId,
            rule.TenantId);
    }
}
