using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetCategoryValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetCategoryRequest request)
    {
        return ValidateFields(request.CategoryName, request.Code, request.ParentCategory, request.Assets);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetCategoryRequest request)
    {
        return ValidateFields(request.CategoryName, request.Code, request.ParentCategory, request.Assets);
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
}
