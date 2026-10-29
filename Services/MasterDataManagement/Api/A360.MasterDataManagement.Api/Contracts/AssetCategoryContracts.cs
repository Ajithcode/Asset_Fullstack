using AssetCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetCategory;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetCategoryRequest(
    string? CategoryName,
    string? Code,
    string? AssetType,
    string? Description,
    MasterDataReferenceDto? ParentCategory,
    List<MasterDataReferenceDto>? Assets,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetCategoryEntity ToEntity()
    {
        return new AssetCategoryEntity
        {
            CategoryName = CategoryName!.Trim(),
            Code = Code!.Trim(),
            AssetType = string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ParentCategory = ParentCategory?.ToReference(),
            Assets = Assets?.Select(x => x.ToReference()).ToList() ?? [],

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetCategoryRequest(
    string? CategoryName,
    string? Code,
    string? AssetType,
    string? Description,
    MasterDataReferenceDto? ParentCategory,
    List<MasterDataReferenceDto>? Assets,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetCategoryEntity category)
    {
        category.CategoryName = CategoryName!.Trim();
        category.Code = Code!.Trim();
        category.AssetType = string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim();
        category.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        category.ParentCategory = ParentCategory?.ToReference();
        category.Assets = Assets?.Select(x => x.ToReference()).ToList() ?? [];

        category.UpdatedBy = UpdatedBy;
        category.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            category.Status = Status;
        }
    }
}

public sealed record UpdateAssetCategoryStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetCategoryResponse(
    string Id,
    string CategoryName,
    string Code,
    string? AssetType,
    string? Description,
    MasterDataReferenceDto? ParentCategory,
    IReadOnlyList<MasterDataReferenceDto> Assets,
    int AssetCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetCategoryResponse FromEntity(AssetCategoryEntity category)
    {
        var assets = (category.Assets ?? []).Select(MasterDataReferenceDto.FromReference).ToList();

        return new AssetCategoryResponse(
            category.Id ?? string.Empty,
            category.CategoryName ?? string.Empty,
            category.Code ?? string.Empty,
            category.AssetType,
            category.Description,
            category.ParentCategory is null
                ? null
                : MasterDataReferenceDto.FromReference(category.ParentCategory),
            assets,
            assets.Count,
            category.CreatedBy,
            category.CreatedAt,
            category.UpdatedBy,
            category.UpdatedAt,
            category.ClientId,
            category.TenantId,
            category.Status,
            category.IsDeleted
        );
    }
}
