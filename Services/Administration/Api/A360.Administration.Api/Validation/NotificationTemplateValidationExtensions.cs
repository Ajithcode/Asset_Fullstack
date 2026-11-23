using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class NotificationTemplateValidationExtensions
{
    private static readonly string[] AllowedChannels = ["Email", "SMS", "In-App", "Push"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive", "Draft"];

    public static Dictionary<string, string[]> Validate(this CreateNotificationTemplateRequest request)
    {
        return ValidateTemplate(
            request.TemplateName,
            request.TemplateCode,
            request.Category,
            request.TriggerEvent,
            request.DeliveryChannels,
            request.Language,
            request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateNotificationTemplateRequest request)
    {
        return ValidateTemplate(
            request.TemplateName,
            request.TemplateCode,
            request.Category,
            request.TriggerEvent,
            request.DeliveryChannels,
            request.Language,
            request.Status);
    }

    // Shared by the create and update requests so both report identical error keys.
    private static Dictionary<string, string[]> ValidateTemplate(
        string? templateName,
        string? templateCode,
        string? category,
        string? triggerEvent,
        List<string>? deliveryChannels,
        string? language,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "TemplateName", templateName);
        Require(errors, "TemplateCode", templateCode);
        Require(errors, "Category", category);
        Require(errors, "TriggerEvent", triggerEvent);
        Require(errors, "Language", language);
        Require(errors, "Status", status);

        if (!string.IsNullOrWhiteSpace(templateCode) && !TemplateCodePattern().IsMatch(templateCode.Trim()))
        {
            errors["TemplateCode"] = ["TemplateCode may only contain letters, digits, hyphens and underscores"];
        }

        var channels = deliveryChannels?.Where(channel => !string.IsNullOrWhiteSpace(channel)).ToList() ?? [];
        if (channels.Count == 0)
        {
            errors["DeliveryChannels"] = ["At least one delivery channel is required"];
        }
        else if (channels.Any(channel => NormalizeChannel(channel) is null))
        {
            errors["DeliveryChannels"] = [$"DeliveryChannels must be from: {string.Join(", ", AllowedChannels)}"];
        }

        if (!string.IsNullOrWhiteSpace(status)
            && !AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AllowedStatuses)}"];
        }

        return errors;
    }

    // Maps "email", "sms", "inapp", "In App", ... onto the canonical channel name, or null if unknown.
    public static string? NormalizeChannel(string? channel)
    {
        var key = new string((channel ?? string.Empty).Where(char.IsLetter).ToArray());

        return AllowedChannels.FirstOrDefault(allowed =>
            string.Equals(new string(allowed.Where(char.IsLetter).ToArray()), key, StringComparison.OrdinalIgnoreCase));
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex TemplateCodePattern();
}
