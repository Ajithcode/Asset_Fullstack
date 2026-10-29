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
