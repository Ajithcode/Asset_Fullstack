using A360.Report.Api.Contracts;

namespace A360.Report.Api.Validation;

public static class ReportValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.ReportName), request.ReportName);
        Require(errors, nameof(request.ChooseFormat), request.ChooseFormat);
        Require(errors, nameof(request.SelectModule), request.SelectModule);

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }
}
