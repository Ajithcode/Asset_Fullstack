using AssetModelEntity = A360.MasterDataManagement.Domain.Entities.AssetModel;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetModelRequest(
    string? ModelName,
    string? ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetModelEntity ToEntity()
    {
        return new AssetModelEntity
        {
            ModelName = ModelName!.Trim(),
            ModelCode = ModelCode!.Trim(),
            Manufacturer = Manufacturer?.ToReference(),
            AssetType = AssetType?.ToReference(),
            Category = Category?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Lifecycle = string.IsNullOrWhiteSpace(Lifecycle) ? null : Lifecycle.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetModelRequest(
    string? ModelName,
    string? ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetModelEntity model)
    {
        model.ModelName = ModelName!.Trim();
        model.ModelCode = ModelCode!.Trim();
        model.Manufacturer = Manufacturer?.ToReference();
        model.AssetType = AssetType?.ToReference();
        model.Category = Category?.ToReference();
        model.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        model.Lifecycle = string.IsNullOrWhiteSpace(Lifecycle) ? null : Lifecycle.Trim();

        model.UpdatedBy = UpdatedBy;
        model.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            model.Status = Status;
        }
    }
}

public sealed record UpdateAssetModelStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetModelResponse(
    string Id,
    string ModelName,
    string ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetModelResponse FromEntity(AssetModelEntity model)
    {
        return new AssetModelResponse(
            model.Id ?? string.Empty,
            model.ModelName ?? string.Empty,
            model.ModelCode ?? string.Empty,
            model.Manufacturer is null
                ? null
                : MasterDataReferenceDto.FromReference(model.Manufacturer),
            model.AssetType is null
                ? null
                : MasterDataReferenceDto.FromReference(model.AssetType),
            model.Category is null
                ? null
                : MasterDataReferenceDto.FromReference(model.Category),
            model.Description,
            model.Lifecycle,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt,
            model.ClientId,
            model.TenantId,
            model.Status,
            model.IsDeleted
        );
    }
}
