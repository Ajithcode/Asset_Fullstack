using AssetSubCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetSubCategory;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetSubCategoryRequest(
    string? CategoryId,
    string? SubCategoryName,
    string? Code,
    string? AssetType,
    string? Description,
    List<MasterDataReferenceDto>? Assets,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetSubCategoryEntity ToEntity()
    {
        return new AssetSubCategoryEntity
        {
            CategoryId = CategoryId!.Trim(),
            SubCategoryName = SubCategoryName!.Trim(),
            Code = Code!.Trim(),
            AssetType = string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
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

public sealed record UpdateAssetSubCategoryRequest(
    string? CategoryId,
    string? SubCategoryName,
    string? Code,
    string? AssetType,
    string? Description,
    List<MasterDataReferenceDto>? Assets,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetSubCategoryEntity subCategory)
    {
        subCategory.CategoryId = CategoryId!.Trim();
        subCategory.SubCategoryName = SubCategoryName!.Trim();
        subCategory.Code = Code!.Trim();
        subCategory.AssetType = string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim();
        subCategory.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        subCategory.Assets = Assets?.Select(x => x.ToReference()).ToList() ?? [];

        subCategory.UpdatedBy = UpdatedBy;
        subCategory.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            subCategory.Status = Status;
        }
    }
}

public sealed record UpdateAssetSubCategoryStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetSubCategoryResponse(
    string SubCategoryId,
    string SubCategoryName,
    string Code,
    string CategoryId,
    string CategoryName,
    string? AssetType,
    string? Description,
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
    public static AssetSubCategoryResponse FromEntity(AssetSubCategoryEntity subCategory)
    {
        var assets = (subCategory.Assets ?? []).Select(MasterDataReferenceDto.FromReference).ToList();

        return new AssetSubCategoryResponse(
            subCategory.Id ?? string.Empty,
            subCategory.SubCategoryName ?? string.Empty,
            subCategory.Code ?? string.Empty,
            subCategory.CategoryId ?? string.Empty,
            subCategory.CategoryName ?? string.Empty,
            subCategory.AssetType,
            subCategory.Description,
            assets,
            assets.Count,
            subCategory.CreatedBy,
            subCategory.CreatedAt,
            subCategory.UpdatedBy,
            subCategory.UpdatedAt,
            subCategory.ClientId,
            subCategory.TenantId,
            subCategory.Status,
            subCategory.IsDeleted
        );
    }
}
