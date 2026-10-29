using AssetTypeEntity = A360.MasterDataManagement.Domain.Entities.AssetType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetTypeRequest(
    string? AssetTypeName,
    string? Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetTypeEntity ToEntity()
    {
        return new AssetTypeEntity
        {
            AssetTypeName = AssetTypeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category?.ToReference(),
            AssetClass = string.IsNullOrWhiteSpace(AssetClass) ? null : AssetClass.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim(),
            DefaultLifetime = DefaultLifetime,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetTypeRequest(
    string? AssetTypeName,
    string? Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetTypeEntity assetType)
    {
        assetType.AssetTypeName = AssetTypeName!.Trim();
        assetType.Code = Code!.Trim();
        assetType.Category = Category?.ToReference();
        assetType.AssetClass = string.IsNullOrWhiteSpace(AssetClass) ? null : AssetClass.Trim();
        assetType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        assetType.DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim();
        assetType.DefaultLifetime = DefaultLifetime;

        assetType.UpdatedBy = UpdatedBy;
        assetType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            assetType.Status = Status;
        }
    }
}

public sealed record UpdateAssetTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetTypeResponse(
    string Id,
    string AssetTypeName,
    string Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetTypeResponse FromEntity(AssetTypeEntity assetType)
    {
        return new AssetTypeResponse(
            assetType.Id ?? string.Empty,
            assetType.AssetTypeName ?? string.Empty,
            assetType.Code ?? string.Empty,
            assetType.Category is null
                ? null
                : MasterDataReferenceDto.FromReference(assetType.Category),
            assetType.AssetClass,
            assetType.Description,
            assetType.DepreciationMethod,
            assetType.DefaultLifetime,
            assetType.CreatedBy,
            assetType.CreatedAt,
            assetType.UpdatedBy,
            assetType.UpdatedAt,
            assetType.ClientId,
            assetType.TenantId,
            assetType.Status,
            assetType.IsDeleted
        );
    }
}
