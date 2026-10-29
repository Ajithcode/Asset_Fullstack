using ItemCategoryEntity = A360.MasterDataManagement.Domain.Entities.ItemCategory;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateItemCategoryRequest(
    string? CategoryName,
    string? Code,
    MasterDataReferenceDto? ParentCategory,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ItemCategoryEntity ToEntity()
    {
        return new ItemCategoryEntity
        {
            CategoryName = CategoryName!.Trim(),
            Code = Code!.Trim(),
            ParentCategory = ParentCategory?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateItemCategoryRequest(
    string? CategoryName,
    string? Code,
    MasterDataReferenceDto? ParentCategory,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ItemCategoryEntity itemCategory)
    {
        itemCategory.CategoryName = CategoryName!.Trim();
        itemCategory.Code = Code!.Trim();
        itemCategory.ParentCategory = ParentCategory?.ToReference();
        itemCategory.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        itemCategory.UpdatedBy = UpdatedBy;
        itemCategory.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            itemCategory.Status = Status;
        }
    }
}

public sealed record UpdateItemCategoryStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ItemCategoryResponse(
    string Id,
    string CategoryName,
    string Code,
    MasterDataReferenceDto? ParentCategory,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ItemCategoryResponse FromEntity(ItemCategoryEntity itemCategory)
    {
        return new ItemCategoryResponse(
            itemCategory.Id ?? string.Empty,
            itemCategory.CategoryName ?? string.Empty,
            itemCategory.Code ?? string.Empty,
            itemCategory.ParentCategory is null
                ? null
                : MasterDataReferenceDto.FromReference(itemCategory.ParentCategory),
            itemCategory.Description,
            itemCategory.CreatedBy,
            itemCategory.CreatedAt,
            itemCategory.UpdatedBy,
            itemCategory.UpdatedAt,
            itemCategory.ClientId,
            itemCategory.TenantId,
            itemCategory.Status,
            itemCategory.IsDeleted
        );
    }
}
