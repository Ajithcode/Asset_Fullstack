using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetCategoryValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    private static readonly Regex TagPrefixPattern = new(
        "^[A-Za-z0-9]{1,6}$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateAssetCategoryRequest request)
    {
        var errors = ValidateFields(request.CategoryName, request.Code, request.ParentCategory, request.Assets);
        ValidateDetails(errors, request.Color, request.UsefulLifeYears, request.TagPrefix, request.Subcategories, request.CustomFields);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetCategoryRequest request)
    {
        var errors = ValidateFields(request.CategoryName, request.Code, request.ParentCategory, request.Assets);
        ValidateDetails(errors, request.Color, request.UsefulLifeYears, request.TagPrefix, request.Subcategories, request.CustomFields);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetCategoryStatusRequest request)
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
        string? categoryName,
        string? code,
        MasterDataReferenceDto? parentCategory,
        List<MasterDataReferenceDto>? assets)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            errors["CategoryName"] =
                ["CategoryName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // The parent is another category in this service, so it is picked by id.
        if (parentCategory is not null && string.IsNullOrWhiteSpace(parentCategory.Id))
        {
            errors["ParentCategory"] =
                ["ParentCategory must have a category id"];
        }

        MasterDataReferenceValidation.ValidateList(errors, "Assets", assets);

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? color,
        int? usefulLifeYears,
        string? tagPrefix,
        List<AssetSubcategoryDto>? subcategories,
        List<AssetCustomFieldDto>? customFields)
    {
        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #1D4ED8"];
        }

        if (usefulLifeYears is < 1 or > 50)
        {
            errors["UsefulLifeYears"] =
                ["UsefulLifeYears must be between 1 and 50"];
        }

        if (!string.IsNullOrWhiteSpace(tagPrefix) && !TagPrefixPattern.IsMatch(tagPrefix.Trim()))
        {
            errors["TagPrefix"] =
                ["TagPrefix must be 1 to 6 letters or digits"];
        }

        if (subcategories is not null)
        {
            if (subcategories.Any(x => x is null || string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name)))
            {
                errors["Subcategories"] =
                    ["Every subcategory needs a code and a name"];
            }
            else if (subcategories.GroupBy(x => x.Code!.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            {
                errors["Subcategories"] =
                    ["Subcategory codes must be unique"];
            }
            else if (subcategories.GroupBy(x => x.Name!.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            {
                errors["Subcategories"] =
                    ["Subcategory names must be unique within a category"];
            }
        }

        AssetCustomFieldValidation.ValidateList(errors, customFields);
    }
}

internal static class AssetCustomFieldValidation
{
    public static void ValidateList(
        Dictionary<string, string[]> errors,
        List<AssetCustomFieldDto>? customFields)
    {
        if (customFields is null)
        {
            return;
        }

        if (customFields.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)))
        {
            errors["CustomFields"] =
                ["Every custom field needs a name"];
        }
        else if (customFields.GroupBy(x => x.Name!.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
        {
            errors["CustomFields"] =
                ["Custom field names must be unique"];
        }
    }
}
