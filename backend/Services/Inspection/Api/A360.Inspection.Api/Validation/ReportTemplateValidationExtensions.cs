using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class ReportTemplateValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateReportTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ReportTitle))
        {
            errors["ReportTitle"] = ["ReportTitle is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateReportTemplateRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ReportTitle))
        {
            errors["ReportTitle"] = ["ReportTitle is required"];
        }

        return errors;
    }
}
