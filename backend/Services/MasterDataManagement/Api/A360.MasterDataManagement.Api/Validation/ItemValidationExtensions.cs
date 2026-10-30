using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ItemValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateItemRequest request)
    {
        return ValidateFields(request.ItemName, request.ItemCode, request.Category, request.Uom, request.CurrentStock, request.ReorderLevel,
            new ItemStockFields(request.MinStock, request.ReorderQty, request.MaxStock, request.Warehouse, request.Store, request.Bin, request.UnitCost, request.ShelfLifeDays));
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateItemRequest request)
    {
        return ValidateFields(request.ItemName, request.ItemCode, request.Category, request.Uom, request.CurrentStock, request.ReorderLevel,
            new ItemStockFields(request.MinStock, request.ReorderQty, request.MaxStock, request.Warehouse, request.Store, request.Bin, request.UnitCost, request.ShelfLifeDays));
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
        double? reorderLevel,
        ItemStockFields stock)
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

        if (stock.MinStock < 0)
        {
            errors["MinStock"] =
                ["MinStock cannot be negative"];
        }

        if (reorderLevel < stock.MinStock)
        {
            errors["ReorderLevel"] =
                ["ReorderLevel cannot be below MinStock"];
        }

        if (stock.ReorderQty < 0)
        {
            errors["ReorderQty"] =
                ["ReorderQty cannot be negative"];
        }

        if (stock.MaxStock is not null && stock.MaxStock < (reorderLevel ?? 0))
        {
            errors["MaxStock"] =
                ["MaxStock must be at least the ReorderLevel"];
        }

        if (stock.UnitCost < 0)
        {
            errors["UnitCost"] =
                ["UnitCost cannot be negative"];
        }

        if (stock.ShelfLifeDays <= 0)
        {
            errors["ShelfLifeDays"] =
                ["ShelfLifeDays must be greater than 0"];
        }

        foreach (var (field, reference) in new[] { ("Warehouse", stock.Warehouse), ("Store", stock.Store), ("Bin", stock.Bin) })
        {
            if (reference is not null && string.IsNullOrWhiteSpace(reference.Id))
            {
                errors[field] =
                    [$"{field} must have an id"];
            }
        }

        if (currentStock > 0 && string.IsNullOrWhiteSpace(stock.Bin?.Id))
        {
            errors["Bin"] =
                ["Bin is required while the item holds stock"];
        }

        return errors;
    }
}

// Stock levels, location and costing checked together with the item.
public sealed record ItemStockFields(
    double? MinStock,
    double? ReorderQty,
    double? MaxStock,
    MasterDataReferenceDto? Warehouse,
    MasterDataReferenceDto? Store,
    MasterDataReferenceDto? Bin,
    double? UnitCost,
    int? ShelfLifeDays);
