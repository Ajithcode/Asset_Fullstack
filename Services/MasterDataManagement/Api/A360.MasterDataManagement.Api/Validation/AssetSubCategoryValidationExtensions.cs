using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetSubCategoryValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetSubCategoryRequest request)
    {
        return ValidateFields(request.CategoryId, request.SubCategoryName, request.Code, request.Assets);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetSubCategoryRequest request)
    {
        return ValidateFields(request.CategoryId, request.SubCategoryName, request.Code, request.Assets);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetSubCategoryStatusRequest request)
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
        string? categoryId,
        string? subCategoryName,
        string? code,
        List<MasterDataReferenceDto>? assets)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(categoryId))
        {
            errors["CategoryId"] =
                ["CategoryId is required"];
        }

        if (string.IsNullOrWhiteSpace(subCategoryName))
        {
            errors["SubCategoryName"] =
                ["SubCategoryName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        MasterDataReferenceValidation.ValidateList(errors, "Assets", assets);

        return errors;
    }
}
