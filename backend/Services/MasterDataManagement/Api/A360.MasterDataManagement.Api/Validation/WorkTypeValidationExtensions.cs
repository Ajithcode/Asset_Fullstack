using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class WorkTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkTypeRequest request)
    {
        return ValidateFields(request.WorkTypeName, request.Code, request.MaintenanceType, request.EstimatedDuration);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkTypeRequest request)
    {
        return ValidateFields(request.WorkTypeName, request.Code, request.MaintenanceType, request.EstimatedDuration);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkTypeStatusRequest request)
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
        string? workTypeName,
        string? code,
        MasterDataReferenceDto? maintenanceType,
        double? estimatedDuration)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(workTypeName))
        {
            errors["WorkTypeName"] =
                ["WorkTypeName is required"];
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

        return errors;
    }
}
