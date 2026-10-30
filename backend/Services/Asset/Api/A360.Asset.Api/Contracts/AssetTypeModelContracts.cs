using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

public sealed record CreateAssetCategoryTypeRequest(
    string? CategoryId,
    string? TypeName,
    string? TypeCode,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetCategoryType ToEntity(CurrentUser? currentUser)
    {
        return new AssetCategoryType
        {
            CategoryId = CategoryId!.Trim(),
            TypeName = TypeName!.Trim(),
            TypeCode = TypeCode!.Trim(),
            Description = ContractText.TrimOrNull(Description),
            Status = ActiveInactiveStatuses.Normalize(Status),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetCategoryTypeRequest(
    string? CategoryId,
    string? TypeName,
    string? TypeCode,
    string? Description,
    string? Status,
    string? ImageUrl)
{
    public void ApplyTo(AssetCategoryType type, CurrentUser? currentUser)
    {
        type.CategoryId = CategoryId!.Trim();
        type.TypeName = TypeName!.Trim();
        type.TypeCode = TypeCode!.Trim();
        type.Description = ContractText.TrimOrNull(Description);
        type.Status = ActiveInactiveStatuses.Normalize(Status);
        type.ImageUrl = ContractText.TrimOrNull(ImageUrl);
        type.UpdatedBy = currentUser?.UserName;
        type.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record AssetCategoryTypeResponse(
    string Id,
    string CategoryId,
    string? CategoryName,
    string TypeName,
    string TypeCode,
    string? Description,
    string? Status,
    string? ImageUrl,
    long ModelCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetCategoryTypeResponse FromEntity(AssetCategoryType type, string? categoryName, long modelCount)
    {
        return new AssetCategoryTypeResponse(
            type.Id,
            type.CategoryId,
            categoryName,
            type.TypeName,
            type.TypeCode,
            type.Description,
            type.Status,
            type.ImageUrl,
            modelCount,
            type.CreatedBy,
            type.CreatedAt,
            type.UpdatedBy,
            type.UpdatedAt,
            type.ClientId,
            type.TenantId);
    }
}

/// <summary>Used for both create and update; models have no image.</summary>
public sealed record SaveAssetModelRequest(
    string? AssetTypeId,
    string? ModelName,
    string? ModelCode,
    string? ManufacturerId,
    string? ManufacturerName,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetModel ToEntity(CurrentUser? currentUser)
    {
        var model = new AssetModel
        {
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        CopyFieldsTo(model);
        return model;
    }

    public void ApplyTo(AssetModel model, CurrentUser? currentUser)
    {
        CopyFieldsTo(model);
        model.UpdatedBy = currentUser?.UserName;
        model.UpdatedAt = DateTime.UtcNow;
    }

    private void CopyFieldsTo(AssetModel model)
    {
        model.AssetTypeId = AssetTypeId!.Trim();
        model.ModelName = ModelName!.Trim();
        model.ModelCode = ModelCode!.Trim();
        model.ManufacturerId = ContractText.TrimOrNull(ManufacturerId);
        model.ManufacturerName = ManufacturerName!.Trim();
        model.Status = ActiveInactiveStatuses.Normalize(Status);
    }
}

public sealed record AssetModelResponse(
    string Id,
    string AssetTypeId,
    string? AssetTypeName,
    string ModelName,
    string ModelCode,
    string? ManufacturerId,
    string ManufacturerName,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetModelResponse FromEntity(AssetModel model, string? assetTypeName)
    {
        return new AssetModelResponse(
            model.Id,
            model.AssetTypeId,
            assetTypeName,
            model.ModelName,
            model.ModelCode,
            model.ManufacturerId,
            model.ManufacturerName,
            model.Status,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt,
            model.ClientId,
            model.TenantId);
    }
}
