using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class TaskCategoryValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateTaskCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] = ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            errors["AssetName"] = ["AssetName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            errors["CategoryName"] = ["CategoryName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateTaskCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] = ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            errors["AssetName"] = ["AssetName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            errors["CategoryName"] = ["CategoryName is required"];
        }

        return errors;
    }
}
