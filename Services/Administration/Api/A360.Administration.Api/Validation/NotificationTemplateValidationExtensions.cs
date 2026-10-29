using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class NotificationTemplateValidationExtensions
{
    private static readonly string[] AllowedChannels = ["Email", "SMS", "In-App", "Push"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive", "Draft"];

    public static Dictionary<string, string[]> Validate(this CreateNotificationTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.TemplateName), request.TemplateName);
        Require(errors, nameof(request.TemplateCode), request.TemplateCode);
        Require(errors, nameof(request.Category), request.Category);
        Require(errors, nameof(request.TriggerEvent), request.TriggerEvent);
        Require(errors, nameof(request.Language), request.Language);
        Require(errors, nameof(request.Status), request.Status);

        if (!string.IsNullOrWhiteSpace(request.TemplateCode) && !TemplateCodePattern().IsMatch(request.TemplateCode.Trim()))
        {
            errors[nameof(request.TemplateCode)] = ["TemplateCode may only contain letters, digits, hyphens and underscores"];
        }

        var channels = request.DeliveryChannels?.Where(channel => !string.IsNullOrWhiteSpace(channel)).ToList() ?? [];
        if (channels.Count == 0)
        {
            errors[nameof(request.DeliveryChannels)] = ["At least one delivery channel is required"];
        }
        else if (channels.Any(channel => NormalizeChannel(channel) is null))
        {
            errors[nameof(request.DeliveryChannels)] = [$"DeliveryChannels must be from: {string.Join(", ", AllowedChannels)}"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !AllowedStatuses.Contains(request.Status.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[nameof(request.Status)] = [$"Status must be one of: {string.Join(", ", AllowedStatuses)}"];
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
