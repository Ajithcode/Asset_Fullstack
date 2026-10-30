using A360.MasterDataManagement.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;
using AssetCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetCategory;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetCategoryRequest(
    string? CategoryName,
    string? Code,
    string? AssetType,
    string? Description,
    MasterDataReferenceDto? ParentCategory,
    List<MasterDataReferenceDto>? Assets,
    string? Icon,
    string? Color,
    string? Image,
    List<AssetSubcategoryDto>? Subcategories,
    string? DepreciationMethod,
    int? UsefulLifeYears,
    string? CostCenter,
    string? TagPrefix,
    string? TrackingMethod,
    string? DefaultCondition,
    List<AssetCustomFieldDto>? CustomFields,
    bool? SubcategoryRequired,
    bool? ShowInMobile,
    bool? IncludeInReports,
    bool? ApprovalForNewAssets,
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
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            Subcategories = Subcategories?.Select(x => x.ToEntity()).ToList() ?? [],
            DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim(),
            UsefulLifeYears = UsefulLifeYears,
            CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim(),
            TagPrefix = string.IsNullOrWhiteSpace(TagPrefix) ? null : TagPrefix.Trim().ToUpperInvariant(),
            TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim(),
            DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim(),
            CustomFields = CustomFields?.Select(x => x.ToEntity()).ToList() ?? [],
            SubcategoryRequired = SubcategoryRequired ?? false,
            ShowInMobile = ShowInMobile ?? true,
            IncludeInReports = IncludeInReports ?? true,
            ApprovalForNewAssets = ApprovalForNewAssets ?? false,

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
    string? Icon,
    string? Color,
    string? Image,
    List<AssetSubcategoryDto>? Subcategories,
    string? DepreciationMethod,
    int? UsefulLifeYears,
    string? CostCenter,
    string? TagPrefix,
    string? TrackingMethod,
    string? DefaultCondition,
    List<AssetCustomFieldDto>? CustomFields,
    bool? SubcategoryRequired,
    bool? ShowInMobile,
    bool? IncludeInReports,
    bool? ApprovalForNewAssets,
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
        category.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        category.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        category.Image = string.IsNullOrWhiteSpace(Image) ? null : Image;
        category.Subcategories = Subcategories?.Select(x => x.ToEntity()).ToList() ?? [];
        category.DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim();
        category.UsefulLifeYears = UsefulLifeYears;
        category.CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim();
        category.TagPrefix = string.IsNullOrWhiteSpace(TagPrefix) ? null : TagPrefix.Trim().ToUpperInvariant();
        category.TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim();
        category.DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim();
        category.CustomFields = CustomFields?.Select(x => x.ToEntity()).ToList() ?? [];
        category.SubcategoryRequired = SubcategoryRequired ?? false;
        category.ShowInMobile = ShowInMobile ?? true;
        category.IncludeInReports = IncludeInReports ?? true;
        category.ApprovalForNewAssets = ApprovalForNewAssets ?? false;

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

public sealed record AssetSubcategoryDto(
    string? Code,
    string? Name,
    string? Icon,
    string? Color,
    string? Status)
{
    public AssetSubcategory ToEntity()
    {
        return new AssetSubcategory
        {
            Code = Code!.Trim().ToUpperInvariant(),
            Name = Name!.Trim(),
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status.Trim()
        };
    }

    public static AssetSubcategoryDto FromEntity(AssetSubcategory subcategory)
    {
        return new AssetSubcategoryDto(
            subcategory.Code,
            subcategory.Name,
            subcategory.Icon,
            subcategory.Color,
            subcategory.Status);
    }
}

// Shared by asset categories and asset types.
public sealed record AssetCustomFieldDto(
    string? Name,
    string? Type,
    bool? Required)
{
    public AssetCustomField ToEntity()
    {
        return new AssetCustomField
        {
            Name = Name!.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            Required = Required ?? false
        };
    }

    public static AssetCustomFieldDto FromEntity(AssetCustomField field)
    {
        return new AssetCustomFieldDto(field.Name, field.Type, field.Required);
    }
}

public sealed record AssetCategoryResponse(
    string Id,
    string CategoryName,
    string Code,
    string? AssetType,
    string? Description,
    MasterDataReferenceDto? ParentCategory,
    IReadOnlyList<MasterDataReferenceDto> Assets,
    int AssetCount,
    string? Icon,
    string? Color,
    string? Image,
    IReadOnlyList<AssetSubcategoryDto> Subcategories,
    string? DepreciationMethod,
    int? UsefulLifeYears,
    string? CostCenter,
    string? TagPrefix,
    string? TrackingMethod,
    string? DefaultCondition,
    IReadOnlyList<AssetCustomFieldDto> CustomFields,
    bool SubcategoryRequired,
    bool ShowInMobile,
    bool IncludeInReports,
    bool ApprovalForNewAssets,
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
            category.Icon,
            category.Color,
            category.Image,
            (category.Subcategories ?? []).Select(AssetSubcategoryDto.FromEntity).ToList(),
            category.DepreciationMethod,
            category.UsefulLifeYears,
            category.CostCenter,
            category.TagPrefix,
            category.TrackingMethod,
            category.DefaultCondition,
            (category.CustomFields ?? []).Select(AssetCustomFieldDto.FromEntity).ToList(),
            category.SubcategoryRequired,
            category.ShowInMobile,
            category.IncludeInReports,
            category.ApprovalForNewAssets,
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
