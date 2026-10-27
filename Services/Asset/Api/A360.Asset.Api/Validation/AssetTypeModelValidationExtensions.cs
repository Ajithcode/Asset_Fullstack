using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetTypeModelValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int CodeMaxLength = 50;
    private const int DescriptionMaxLength = 500;

    public static Dictionary<string, string[]> Validate(this CreateAssetCategoryTypeRequest request)
    {
        return ValidateType(request.CategoryId, request.TypeName, request.TypeCode, request.Description, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetCategoryTypeRequest request)
    {
        return ValidateType(request.CategoryId, request.TypeName, request.TypeCode, request.Description, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this SaveAssetModelRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "AssetTypeId", request.AssetTypeId);
        RequireText(errors, "ModelName", request.ModelName, NameMaxLength);
        RequireText(errors, "ModelCode", request.ModelCode, CodeMaxLength);
        RequireText(errors, "ManufacturerName", request.ManufacturerName, NameMaxLength);
        ValidateStatus(errors, request.Status);

        return errors;
    }

    private static Dictionary<string, string[]> ValidateType(string? categoryId, string? typeName, string? typeCode, string? description, string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "CategoryId", categoryId);
        RequireText(errors, "TypeName", typeName, NameMaxLength);
        RequireText(errors, "TypeCode", typeCode, CodeMaxLength);

        if (description?.Trim().Length > DescriptionMaxLength)
        {
            errors["Description"] = [$"Description cannot be longer than {DescriptionMaxLength} characters"];
        }

        ValidateStatus(errors, status);

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
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

    private static void ValidateStatus(Dictionary<string, string[]> errors, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && !ActiveInactiveStatuses.IsValid(status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }
    }
}
