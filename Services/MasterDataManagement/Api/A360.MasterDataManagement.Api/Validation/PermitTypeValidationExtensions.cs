using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class PermitTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePermitTypeRequest request)
    {
        return ValidateFields(request.PermitTypeName, request.Code, request.Category, request.ValidTillDays, request.ApprovalLevels, request.ApproverRoles, request.MaxExtensionDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermitTypeRequest request)
    {
        return ValidateFields(request.PermitTypeName, request.Code, request.Category, request.ValidTillDays, request.ApprovalLevels, request.ApproverRoles, request.MaxExtensionDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermitTypeStatusRequest request)
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
        string? permitTypeName,
        string? code,
        string? category,
        int? validTillDays,
        int? approvalLevels,
        List<string>? approverRoles,
        int? maxExtensionDays)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(permitTypeName))
        {
            errors["PermitTypeName"] =
                ["PermitTypeName is required"];
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

        if (validTillDays < 1)
        {
            errors["ValidTillDays"] =
                ["ValidTillDays must be at least 1"];
        }

        if (approvalLevels is < 1 or > 5)
        {
            errors["ApprovalLevels"] =
                ["ApprovalLevels must be between 1 and 5"];
        }
        else if (approvalLevels is not null && (approverRoles?.Count(x => !string.IsNullOrWhiteSpace(x)) ?? 0) < approvalLevels)
        {
            errors["ApproverRoles"] =
                ["ApproverRoles needs at least one role per approval level"];
        }

        if (maxExtensionDays < 0 || (validTillDays is not null && maxExtensionDays > validTillDays * 2))
        {
            errors["MaxExtensionDays"] =
                ["MaxExtensionDays must be between 0 and twice ValidTillDays"];
        }

        return errors;
    }
}
