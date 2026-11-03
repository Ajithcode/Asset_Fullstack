using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetComponentValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int CodeMaxLength = 50;
    private const int DescriptionMaxLength = 500;

    public static Dictionary<string, string[]> Validate(this SaveAssetComponentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "ComponentName", request.ComponentName, NameMaxLength);
        RequireText(errors, "ComponentTag", request.ComponentTag, CodeMaxLength);

        if (request.ExpectedReplacementDate is { } replacement
            && request.InstalledDate is { } installed
            && replacement.Date < installed.Date)
        {
            errors["ExpectedReplacementDate"] = ["ExpectedReplacementDate cannot be before InstalledDate"];
        }

        if (request.LifecycleUsed < 0)
        {
            errors["LifecycleUsed"] = ["LifecycleUsed cannot be negative"];
        }

        if (request.LifecycleRatedLife < 0)
        {
            errors["LifecycleRatedLife"] = ["LifecycleRatedLife cannot be negative"];
        }

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors["Description"] = [$"Description cannot be longer than {DescriptionMaxLength} characters"];
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
