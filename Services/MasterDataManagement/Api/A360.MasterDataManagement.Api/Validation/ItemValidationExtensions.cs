using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ItemValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateItemRequest request)
    {
        return ValidateFields(request.ItemName, request.ItemCode, request.Category, request.Uom, request.CurrentStock, request.ReorderLevel);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateItemRequest request)
    {
        return ValidateFields(request.ItemName, request.ItemCode, request.Category, request.Uom, request.CurrentStock, request.ReorderLevel);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateItemStatusRequest request)
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
        string? itemName,
        string? itemCode,
        MasterDataReferenceDto? category,
        MasterDataReferenceDto? uom,
        double? currentStock,
        double? reorderLevel)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(itemName))
        {
            errors["ItemName"] =
                ["ItemName is required"];
        }

        if (string.IsNullOrWhiteSpace(itemCode))
        {
            errors["ItemCode"] =
                ["ItemCode is required"];
        }

        if (string.IsNullOrWhiteSpace(category?.Id))
        {
            errors["Category"] =
                ["Category is required: pick an item category by id"];
        }

        if (string.IsNullOrWhiteSpace(uom?.Id))
        {
            errors["Uom"] =
                ["Uom is required: pick a unit of measure by id"];
        }

        if (currentStock < 0)
        {
            errors["CurrentStock"] =
                ["CurrentStock cannot be negative"];
        }

        if (reorderLevel < 0)
        {
            errors["ReorderLevel"] =
                ["ReorderLevel cannot be negative"];
        }

        return errors;
    }
}
