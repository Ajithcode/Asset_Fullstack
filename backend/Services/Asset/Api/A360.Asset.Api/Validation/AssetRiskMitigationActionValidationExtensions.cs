using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetRiskMitigationActionValidationExtensions
{
    private const int ActionMaxLength = 1000;
    private const int OwnerMaxLength = 200;

    public static Dictionary<string, string[]> Validate(this CreateAssetRiskMitigationActionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId))
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required"];
        }

        RequireText(errors, "Action", request.Action, ActionMaxLength);
        RequireText(errors, "Owner", request.Owner, OwnerMaxLength);

        if (request.DueDate is null)
        {
            errors["DueDate"] = ["DueDate is required"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !AssetRiskMitigationStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AssetRiskMitigationStatuses.All)}"];
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedCriticality) && !AssetCriticalityLevels.IsValid(request.ExpectedCriticality))
        {
            errors["ExpectedCriticality"] = [$"ExpectedCriticality must be one of: {string.Join(", ", AssetCriticalityLevels.All)}"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetRiskMitigationActionStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!AssetRiskMitigationStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AssetRiskMitigationStatuses.All)}"];
        }

        return errors;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
