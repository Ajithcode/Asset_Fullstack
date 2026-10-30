using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class NotificationTemplateValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateNotificationTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            errors["TemplateName"] = ["TemplateName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Event))
        {
            errors["Event"] = ["Event is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            errors["Channel"] = ["Channel is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateNotificationTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            errors["TemplateName"] = ["TemplateName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Event))
        {
            errors["Event"] = ["Event is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            errors["Channel"] = ["Channel is required"];
        }

        return errors;
    }
}
