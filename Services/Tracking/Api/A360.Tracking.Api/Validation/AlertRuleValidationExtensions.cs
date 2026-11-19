using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class AlertRuleValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int MaxDwellMinutes = 24 * 60;

    public static Dictionary<string, string[]> Validate(this CreateAlertRuleRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "Name", request.Name, NameMaxLength);
        RequireText(errors, "AssetGroup", request.AssetGroup, NameMaxLength);

        if (string.IsNullOrWhiteSpace(request.GeofenceId) && string.IsNullOrWhiteSpace(request.MapZoneId))
        {
            errors["GeofenceId"] = ["GeofenceId or MapZoneId is required"];
        }

        if (request.NotifyIntervalSeconds is < 1 or > AlertNotifyDefaults.MaxIntervalSeconds)
        {
            errors["NotifyIntervalSeconds"] = [$"NotifyIntervalSeconds must be between 1 and {AlertNotifyDefaults.MaxIntervalSeconds}"];
        }

        if (request.NotifyRepeat is < 0 or > AlertNotifyDefaults.MaxRepeat)
        {
            errors["NotifyRepeat"] = [$"NotifyRepeat must be between 0 (until acknowledged) and {AlertNotifyDefaults.MaxRepeat}"];
        }

        OneOf(errors, "Trigger", request.Trigger, AlertRuleTrigger.All, required: true);
        OneOf(errors, "Severity", request.Severity, AlertRuleSeverity.All, required: true);
        OneOf(errors, "Condition", request.Condition, AlertRuleCondition.All, required: false);
        OneOf(errors, "Status", request.Status, AlertRuleStatus.All, required: false);

        var isDwellTrigger = ContractText.Canonical(request.Trigger, AlertRuleTrigger.All) == AlertRuleTrigger.RemainsIn;
        if (isDwellTrigger && request.DwellMinutes is null)
        {
            errors["DwellMinutes"] = ["DwellMinutes is required when Trigger is \"When asset remains in zone\""];
        }
        else if (request.DwellMinutes is < 1 or > MaxDwellMinutes)
        {
            errors["DwellMinutes"] = [$"DwellMinutes must be between 1 and {MaxDwellMinutes}"];
        }

        if (request.NotifyTeam == true)
        {
            RequireText(errors, "NotifyTeamName", request.NotifyTeamName, NameMaxLength);
        }

        if (request.StartWorkflow == true)
        {
            RequireText(errors, "WorkflowName", request.WorkflowName, NameMaxLength);
        }

        var invalidChannels = (request.NotificationChannels ?? [])
            .Where(channel => ContractText.Canonical(channel, AlertNotificationChannel.All) is null)
            .ToList();
        if (invalidChannels.Count > 0)
        {
            errors["NotificationChannels"] = [$"NotificationChannels must each be one of: {string.Join(", ", AlertNotificationChannel.All)}"];
        }

        if (request.Description?.Trim().Length > 500)
        {
            errors["Description"] = ["Description cannot be longer than 500 characters"];
        }

        var condition = ContractText.Canonical(request.Condition, AlertRuleCondition.All);
        if (condition == AlertRuleCondition.Custom)
        {
            if ((request.ScheduleDays ?? []).Count(d => ContractText.Canonical(d, AlertRuleCondition.Weekdays) is not null) == 0)
            {
                errors["ScheduleDays"] = ["Choose at least one weekday for a custom schedule"];
            }

            if (!TimeSpan.TryParse(request.ScheduleFrom, out _) || !TimeSpan.TryParse(request.ScheduleTo, out _))
            {
                errors["ScheduleFrom"] = ["A custom schedule needs a start and end time (HH:mm)"];
            }
        }

        if (ContractText.Canonical(request.Trigger, AlertRuleTrigger.All) == AlertRuleTrigger.UnauthorizedEntry && (request.AuthorizedGroups ?? []).All(string.IsNullOrWhiteSpace))
        {
            errors["AuthorizedGroups"] = ["Choose the asset groups that are allowed to enter"];
        }

        if (request.DebounceSeconds is < 0 or > 3600)
        {
            errors["DebounceSeconds"] = ["DebounceSeconds must be between 0 and 3600"];
        }

        if (request.CooldownMinutes is < 0 or > MaxDwellMinutes)
        {
            errors["CooldownMinutes"] = [$"CooldownMinutes must be between 0 and {MaxDwellMinutes}"];
        }

        if (request.EscalateAfterMinutes is > 0 && string.IsNullOrWhiteSpace(request.EscalateTo))
        {
            errors["EscalateTo"] = ["Choose who the alert is escalated to"];
        }

        if (request.EffectiveFrom is not null && request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
        {
            errors["EffectiveTo"] = ["The end date must be after the start date"];
        }

        if (request.CreateAlert == false && request.NotifyTeam != true && request.StartWorkflow != true)
        {
            errors["Actions"] = ["At least one action must be selected: CreateAlert, NotifyTeam or StartWorkflow"];
        }

        return errors;
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
        }
        else if (ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
