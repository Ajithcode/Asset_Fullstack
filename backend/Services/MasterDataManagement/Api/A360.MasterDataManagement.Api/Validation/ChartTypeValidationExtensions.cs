using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ChartTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateChartTypeRequest request)
    {
        return ValidateFields(request.ChartTypeName, request.Code, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChartTypeRequest request)
    {
        return ValidateFields(request.ChartTypeName, request.Code, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChartTypeStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? chartTypeName,
        string? code,
        string? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(chartTypeName))
        {
            errors["ChartTypeName"] =
                ["ChartTypeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        return errors;
    }
}
