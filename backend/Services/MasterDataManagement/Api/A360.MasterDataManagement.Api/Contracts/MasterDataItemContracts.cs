using MasterDataItemEntity = A360.MasterDataManagement.Domain.Entities.MasterDataItem;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateMasterDataItemRequest(
    string? MasterType,
    string? Code,
    string? Name,
    string? Description,
    int SortOrder,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public MasterDataItemEntity ToEntity()
    {
        return new MasterDataItemEntity
        {
            MasterType = MasterType!,
            Code = Code!,
            Name = Name!,
            Description = Description,
            SortOrder = SortOrder,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateMasterDataItemRequest(
    string? MasterType,
    string? Code,
    string? Name,
    string? Description,
    int SortOrder,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(MasterDataItemEntity masterDataItem)
    {
        masterDataItem.MasterType = MasterType!;
        masterDataItem.Code = Code!;
        masterDataItem.Name = Name!;
        masterDataItem.Description = Description;
        masterDataItem.SortOrder = SortOrder;

        masterDataItem.UpdatedBy = UpdatedBy;
        masterDataItem.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            masterDataItem.Status = Status;
        }
    }
}

public sealed record MasterDataItemResponse(
    string Id,
    string MasterType,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static MasterDataItemResponse FromEntity(MasterDataItemEntity masterDataItem)
    {
        return new MasterDataItemResponse(
            masterDataItem.Id ?? string.Empty,
            masterDataItem.MasterType ?? string.Empty,
            masterDataItem.Code ?? string.Empty,
            masterDataItem.Name ?? string.Empty,
            masterDataItem.Description,
            masterDataItem.SortOrder,
            masterDataItem.CreatedBy,
            masterDataItem.CreatedAt,
            masterDataItem.UpdatedBy,
            masterDataItem.UpdatedAt,
            masterDataItem.ClientId,
            masterDataItem.TenantId,
            masterDataItem.Status,
            masterDataItem.IsDeleted
        );
    }
}
