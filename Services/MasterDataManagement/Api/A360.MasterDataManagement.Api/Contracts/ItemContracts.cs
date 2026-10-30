using ItemEntity = A360.MasterDataManagement.Domain.Entities.Item;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateItemRequest(
    string? ItemName,
    string? ItemCode,
    string? PartNumber,
    MasterDataReferenceDto? Category,
    MasterDataReferenceDto? Uom,
    string? Description,
    string? ImageUrl,
    double? CurrentStock,
    double? ReorderLevel,
    double? MinStock,
    double? ReorderQty,
    double? MaxStock,
    MasterDataReferenceDto? Warehouse,
    MasterDataReferenceDto? Store,
    MasterDataReferenceDto? Bin,
    double? UnitCost,
    string? Currency,
    string? ValuationMethod,
    string? Manufacturer,
    string? Specification,
    int? ShelfLifeDays,
    bool? Hazardous,
    bool? Serialized,
    bool? BatchTracked,
    bool? Purchasable,
    bool? Issuable,
    bool? CriticalSpare,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ItemEntity ToEntity()
    {
        return new ItemEntity
        {
            ItemName = ItemName!.Trim(),
            ItemCode = ItemCode!.Trim(),
            PartNumber = string.IsNullOrWhiteSpace(PartNumber) ? null : PartNumber.Trim(),
            Category = Category?.ToReference(),
            Uom = Uom?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim(),
            CurrentStock = CurrentStock,
            ReorderLevel = ReorderLevel,
            MinStock = MinStock,
            ReorderQty = ReorderQty,
            MaxStock = MaxStock,
            Warehouse = Warehouse?.ToReference(),
            Store = Store?.ToReference(),
            Bin = Bin?.ToReference(),
            UnitCost = UnitCost,
            Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim(),
            ValuationMethod = string.IsNullOrWhiteSpace(ValuationMethod) ? null : ValuationMethod.Trim(),
            Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim(),
            Specification = string.IsNullOrWhiteSpace(Specification) ? null : Specification.Trim(),
            ShelfLifeDays = ShelfLifeDays,
            Hazardous = Hazardous ?? false,
            Serialized = Serialized ?? false,
            BatchTracked = BatchTracked ?? false,
            Purchasable = Purchasable ?? true,
            Issuable = Issuable ?? true,
            CriticalSpare = CriticalSpare ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateItemRequest(
    string? ItemName,
    string? ItemCode,
    string? PartNumber,
    MasterDataReferenceDto? Category,
    MasterDataReferenceDto? Uom,
    string? Description,
    string? ImageUrl,
    double? CurrentStock,
    double? ReorderLevel,
    double? MinStock,
    double? ReorderQty,
    double? MaxStock,
    MasterDataReferenceDto? Warehouse,
    MasterDataReferenceDto? Store,
    MasterDataReferenceDto? Bin,
    double? UnitCost,
    string? Currency,
    string? ValuationMethod,
    string? Manufacturer,
    string? Specification,
    int? ShelfLifeDays,
    bool? Hazardous,
    bool? Serialized,
    bool? BatchTracked,
    bool? Purchasable,
    bool? Issuable,
    bool? CriticalSpare,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ItemEntity item)
    {
        item.ItemName = ItemName!.Trim();
        item.ItemCode = ItemCode!.Trim();
        item.PartNumber = string.IsNullOrWhiteSpace(PartNumber) ? null : PartNumber.Trim();
        item.Category = Category?.ToReference();
        item.Uom = Uom?.ToReference();
        item.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        item.ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim();
        item.CurrentStock = CurrentStock;
        item.ReorderLevel = ReorderLevel;
        item.MinStock = MinStock;
        item.ReorderQty = ReorderQty;
        item.MaxStock = MaxStock;
        item.Warehouse = Warehouse?.ToReference();
        item.Store = Store?.ToReference();
        item.Bin = Bin?.ToReference();
        item.UnitCost = UnitCost;
        item.Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim();
        item.ValuationMethod = string.IsNullOrWhiteSpace(ValuationMethod) ? null : ValuationMethod.Trim();
        item.Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim();
        item.Specification = string.IsNullOrWhiteSpace(Specification) ? null : Specification.Trim();
        item.ShelfLifeDays = ShelfLifeDays;
        item.Hazardous = Hazardous ?? false;
        item.Serialized = Serialized ?? false;
        item.BatchTracked = BatchTracked ?? false;
        item.Purchasable = Purchasable ?? true;
        item.Issuable = Issuable ?? true;
        item.CriticalSpare = CriticalSpare ?? false;

        item.UpdatedBy = UpdatedBy;
        item.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            item.Status = Status;
        }
    }
}

public sealed record UpdateItemStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ItemResponse(
    string Id,
    string ItemName,
    string ItemCode,
    string? PartNumber,
    MasterDataReferenceDto? Category,
    MasterDataReferenceDto? Uom,
    string? Description,
    string? ImageUrl,
    double? CurrentStock,
    double? ReorderLevel,
    double? MinStock,
    double? ReorderQty,
    double? MaxStock,
    MasterDataReferenceDto? Warehouse,
    MasterDataReferenceDto? Store,
    MasterDataReferenceDto? Bin,
    double? UnitCost,
    string? Currency,
    string? ValuationMethod,
    string? Manufacturer,
    string? Specification,
    int? ShelfLifeDays,
    bool Hazardous,
    bool Serialized,
    bool BatchTracked,
    bool Purchasable,
    bool Issuable,
    bool CriticalSpare,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ItemResponse FromEntity(ItemEntity item)
    {
        return new ItemResponse(
            item.Id ?? string.Empty,
            item.ItemName ?? string.Empty,
            item.ItemCode ?? string.Empty,
            item.PartNumber,
            item.Category is null
                ? null
                : MasterDataReferenceDto.FromReference(item.Category),
            item.Uom is null
                ? null
                : MasterDataReferenceDto.FromReference(item.Uom),
            item.Description,
            item.ImageUrl,
            item.CurrentStock,
            item.ReorderLevel,
            item.MinStock,
            item.ReorderQty,
            item.MaxStock,
            item.Warehouse is null
                ? null
                : MasterDataReferenceDto.FromReference(item.Warehouse),
            item.Store is null
                ? null
                : MasterDataReferenceDto.FromReference(item.Store),
            item.Bin is null
                ? null
                : MasterDataReferenceDto.FromReference(item.Bin),
            item.UnitCost,
            item.Currency,
            item.ValuationMethod,
            item.Manufacturer,
            item.Specification,
            item.ShelfLifeDays,
            item.Hazardous,
            item.Serialized,
            item.BatchTracked,
            item.Purchasable,
            item.Issuable,
            item.CriticalSpare,
            item.CreatedBy,
            item.CreatedAt,
            item.UpdatedBy,
            item.UpdatedAt,
            item.ClientId,
            item.TenantId,
            item.Status,
            item.IsDeleted
        );
    }
}
