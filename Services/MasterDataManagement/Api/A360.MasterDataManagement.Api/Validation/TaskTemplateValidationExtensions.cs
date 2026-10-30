using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class TaskTemplateValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTaskTemplateRequest request)
    {
        return ValidateFields(request.TemplateName, request.Code, request.MaintenanceType, request.EstimatedDuration, request.Tasks);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTaskTemplateRequest request)
    {
        return ValidateFields(request.TemplateName, request.Code, request.MaintenanceType, request.EstimatedDuration, request.Tasks);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTaskTemplateStatusRequest request)
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
        string? templateName,
        string? code,
        MasterDataReferenceDto? maintenanceType,
        double? estimatedDuration,
        List<MasterDataStepDto>? tasks)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(templateName))
        {
            errors["TemplateName"] =
                ["TemplateName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(maintenanceType?.Id))
        {
            errors["MaintenanceType"] =
                ["MaintenanceType is required: pick a maintenance type by id"];
        }

        if (estimatedDuration < 0)
        {
            errors["EstimatedDuration"] =
                ["EstimatedDuration cannot be negative"];
        }

        if (tasks?.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)) == true)
        {
            errors["Tasks"] =
                ["Every entry in Tasks needs a name"];
        }

        if (tasks?.Any(x => x is not null && x.DurationMinutes is not null && x.DurationMinutes <= 0) == true)
        {
            errors["Tasks"] =
                ["Task minutes must be above 0"];
        }

        return errors;
    }
}
