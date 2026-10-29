using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ItemCategoryValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateItemCategoryRequest request)
    {
        return ValidateFields(request.CategoryName, request.Code, request.ParentCategory);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateItemCategoryRequest request)
    {
        return ValidateFields(request.CategoryName, request.Code, request.ParentCategory);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateItemCategoryStatusRequest request)
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
        MasterDataReferenceDto? parentCategory)
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

        if (parentCategory is not null && string.IsNullOrWhiteSpace(parentCategory.Id))
        {
            errors["ParentCategory"] =
                ["ParentCategory must have an item category id"];
        }

        return errors;
    }
}
