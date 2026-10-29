using BinEntity = A360.MasterDataManagement.Domain.Entities.Bin;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateBinRequest(
    string? BinName,
    string? Code,
    MasterDataReferenceDto? Store,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public BinEntity ToEntity()
    {
        return new BinEntity
        {
            BinName = BinName!.Trim(),
            Code = Code!.Trim(),
            Store = Store?.ToReference(),
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

public sealed record UpdateBinRequest(
    string? BinName,
    string? Code,
    MasterDataReferenceDto? Store,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(BinEntity bin)
    {
        bin.BinName = BinName!.Trim();
        bin.Code = Code!.Trim();
        bin.Store = Store?.ToReference();
        bin.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        bin.UpdatedBy = UpdatedBy;
        bin.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            bin.Status = Status;
        }
    }
}

public sealed record UpdateBinStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record BinResponse(
    string Id,
    string BinName,
    string Code,
    MasterDataReferenceDto? Store,
    MasterDataReferenceDto? Warehouse,
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
    public static BinResponse FromEntity(BinEntity bin)
    {
        return new BinResponse(
            bin.Id ?? string.Empty,
            bin.BinName ?? string.Empty,
            bin.Code ?? string.Empty,
            bin.Store is null
                ? null
                : MasterDataReferenceDto.FromReference(bin.Store),
            bin.Warehouse is null
                ? null
                : MasterDataReferenceDto.FromReference(bin.Warehouse),
            bin.Description,
            bin.CreatedBy,
            bin.CreatedAt,
            bin.UpdatedBy,
            bin.UpdatedAt,
            bin.ClientId,
            bin.TenantId,
            bin.Status,
            bin.IsDeleted
        );
    }
}
