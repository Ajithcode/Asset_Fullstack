using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetHierarchyValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int CodeMaxLength = 50;

    public static Dictionary<string, string[]> Validate(this SaveAssetHierarchyItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ItemKind))
        {
            errors["ItemKind"] = ["ItemKind is required"];
        }
        else if (request.ItemKind.Trim().Length > CodeMaxLength)
        {
            errors["ItemKind"] = [$"ItemKind cannot be longer than {CodeMaxLength} characters"];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] = ["Name is required"];
        }
        else if (request.Name.Trim().Length > NameMaxLength)
        {
            errors["Name"] = [$"Name cannot be longer than {NameMaxLength} characters"];
        }

        if (request.AssetTag?.Trim().Length > CodeMaxLength)
        {
            errors["AssetTag"] = [$"AssetTag cannot be longer than {CodeMaxLength} characters"];
        }

        if (request.ManufacturerName?.Trim().Length > NameMaxLength)
        {
            errors["ManufacturerName"] = [$"ManufacturerName cannot be longer than {NameMaxLength} characters"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !ActiveInactiveStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }

        return errors;
    }
}
