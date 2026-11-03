using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetCategoryValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int CodeMaxLength = 50;
    private const int DescriptionMaxLength = 500;

    public static Dictionary<string, string[]> Validate(this CreateAssetCategoryRequest request)
    {
        return ValidateFields(request.Name, request.Code, request.Description, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetCategoryRequest request)
    {
        return ValidateFields(request.Name, request.Code, request.Description, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this CreateAssetCategoryCustomFieldRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FieldName))
        {
            errors["FieldName"] = ["FieldName is required"];
        }
        else if (request.FieldName.Trim().Length > NameMaxLength)
        {
            errors["FieldName"] = [$"FieldName cannot be longer than {NameMaxLength} characters"];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["Type"] = ["Type is required"];
        }
        else if (AssetCategoryCustomFieldTypes.Normalize(request.Type) is null)
        {
            errors["Type"] = [$"Type must be one of: {string.Join(", ", AssetCategoryCustomFieldTypes.All)}"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateAssetCategoryAttributeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Attribute))
        {
            errors["Attribute"] = ["Attribute is required"];
        }
        else if (request.Attribute.Trim().Length > NameMaxLength)
        {
            errors["Attribute"] = [$"Attribute cannot be longer than {NameMaxLength} characters"];
        }

        if (request.Unit?.Trim().Length > CodeMaxLength)
        {
            errors["Unit"] = [$"Unit cannot be longer than {CodeMaxLength} characters"];
        }

        if (request.ExampleValue?.Trim().Length > NameMaxLength)
        {
            errors["ExampleValue"] = [$"ExampleValue cannot be longer than {NameMaxLength} characters"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(string? name, string? code, string? description, string? status)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["Name"] = ["Name is required"];
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors["Name"] = [$"Name cannot be longer than {NameMaxLength} characters"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] = ["Code is required"];
        }
        else if (code.Trim().Length > CodeMaxLength)
        {
            errors["Code"] = [$"Code cannot be longer than {CodeMaxLength} characters"];
        }

        if (description?.Trim().Length > DescriptionMaxLength)
        {
            errors["Description"] = [$"Description cannot be longer than {DescriptionMaxLength} characters"];
        }

        if (!string.IsNullOrWhiteSpace(status) && !ActiveInactiveStatuses.IsValid(status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }

        return errors;
    }
}
