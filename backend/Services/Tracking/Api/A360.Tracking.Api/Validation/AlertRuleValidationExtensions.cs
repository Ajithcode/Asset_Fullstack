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

        if (string.IsNullOrWhiteSpace(request.GeofenceId))
        {
            errors["GeofenceId"] = ["GeofenceId is required"];
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
