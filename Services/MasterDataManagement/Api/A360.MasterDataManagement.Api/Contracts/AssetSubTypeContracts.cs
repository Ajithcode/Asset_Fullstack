using A360.MasterDataManagement.Domain.Entities;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetSubTypeRequest(
    string? SubTypeName,
    string? Code,
    MasterDataReferenceDto? AssetType,
    string? Description,
    string? SettingsJson,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetSubType ToEntity()
    {
        return new AssetSubType
        {
            SubTypeName = SubTypeName!.Trim(),
            Code = Code!.Trim(),
            AssetType = AssetType?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            SettingsJson = string.IsNullOrWhiteSpace(SettingsJson) ? null : SettingsJson,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetSubTypeRequest(
    string? SubTypeName,
    string? Code,
    MasterDataReferenceDto? AssetType,
    string? Description,
    string? SettingsJson,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetSubType subType)
    {
        subType.SubTypeName = SubTypeName!.Trim();
        subType.Code = Code!.Trim();
        subType.AssetType = AssetType?.ToReference();
        subType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        subType.SettingsJson = string.IsNullOrWhiteSpace(SettingsJson) ? null : SettingsJson;

        subType.UpdatedBy = UpdatedBy;
        subType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            subType.Status = Status;
        }
    }
}

public sealed record UpdateAssetSubTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetSubTypeResponse(
    string Id,
    string SubTypeName,
    string Code,
    MasterDataReferenceDto? AssetType,
    string? Description,
    string? SettingsJson,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetSubTypeResponse FromEntity(AssetSubType subType)
    {
        return new AssetSubTypeResponse(
            subType.Id ?? string.Empty,
            subType.SubTypeName ?? string.Empty,
            subType.Code ?? string.Empty,
            subType.AssetType is null ? null : MasterDataReferenceDto.FromReference(subType.AssetType),
            subType.Description,
            subType.SettingsJson,
            subType.CreatedBy,
            subType.CreatedAt,
            subType.UpdatedBy,
            subType.UpdatedAt,
            subType.ClientId,
            subType.TenantId,
            subType.Status,
            subType.IsDeleted);
    }
}
