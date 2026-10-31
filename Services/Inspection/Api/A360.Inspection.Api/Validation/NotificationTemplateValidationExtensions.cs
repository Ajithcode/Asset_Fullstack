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

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateNotificationTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            errors["TemplateName"] = ["TemplateName is required"];
        }

        return errors;
    }
}
