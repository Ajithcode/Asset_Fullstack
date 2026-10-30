using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>Used for both create and edit.</summary>
/// <remarks>Asset type and model names are taken from their masters, so only their ids are sent.</remarks>
public sealed record SaveAssetHierarchyItemRequest(
    string? ItemKind,
    string? ParentId,
    string? Name,
    string? AssetTag,
    string? AssetTypeId,
    string? ModelId,
    string? ManufacturerId,
    string? ManufacturerName,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetHierarchyItem ToEntity(CurrentUser? currentUser)
    {
        var item = new AssetHierarchyItem
        {
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        CopyFieldsTo(item);
        return item;
    }

    public void ApplyTo(AssetHierarchyItem item, CurrentUser? currentUser)
    {
        CopyFieldsTo(item);
        item.UpdatedBy = currentUser?.UserName;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private void CopyFieldsTo(AssetHierarchyItem item)
    {
        item.ItemKind = ItemKind!.Trim();
        item.ParentId = ContractText.TrimOrNull(ParentId);
        item.Name = Name!.Trim();
        item.AssetTag = ContractText.TrimOrNull(AssetTag);
        item.AssetTypeId = ContractText.TrimOrNull(AssetTypeId);
        item.ModelId = ContractText.TrimOrNull(ModelId);
        item.ManufacturerId = ContractText.TrimOrNull(ManufacturerId);
        item.ManufacturerName = ContractText.TrimOrNull(ManufacturerName);
        item.Status = ActiveInactiveStatuses.Normalize(Status);
    }
}

public sealed record AssetHierarchyItemResponse(
    string Id,
    string ItemKind,
    string? ParentId,
    string? ParentName,
    string Name,
    string? AssetTag,
    string? AssetTypeId,
    string? AssetTypeName,
    string? ModelId,
    string? ModelName,
    string? ManufacturerId,
    string? ManufacturerName,
    string? Status,
    int ChildCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetHierarchyItemResponse FromEntity(AssetHierarchyItem item, string? parentName, int childCount)
    {
        return new AssetHierarchyItemResponse(
            item.Id,
            item.ItemKind,
            item.ParentId,
            parentName,
            item.Name,
            item.AssetTag,
            item.AssetTypeId,
            item.AssetTypeName,
            item.ModelId,
            item.ModelName,
            item.ManufacturerId,
            item.ManufacturerName,
            item.Status,
            childCount,
            item.CreatedBy,
            item.CreatedAt,
            item.UpdatedBy,
            item.UpdatedAt,
            item.ClientId,
            item.TenantId);
    }
}

/// <summary>An item with its children nested, for showing the hierarchy as a tree.</summary>
public sealed record AssetHierarchyNodeResponse(
    string Id,
    string ItemKind,
    string Name,
    string? AssetTag,
    string? AssetTypeName,
    string? ModelName,
    string? ManufacturerName,
    string? Status,
    IReadOnlyList<AssetHierarchyNodeResponse> Children);
