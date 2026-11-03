using StoreEntity = A360.MasterDataManagement.Domain.Entities.Store;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateStoreRequest(
    string? StoreName,
    string? Code,
    MasterDataReferenceDto? Warehouse,
    string? Description,
    string? StoreType,
    MasterDataReferenceDto? Keeper,
    bool? TemperatureControlled,
    bool? RestrictedAccess,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public StoreEntity ToEntity()
    {
        return new StoreEntity
        {
            StoreName = StoreName!.Trim(),
            Code = Code!.Trim(),
            Warehouse = Warehouse?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            StoreType = string.IsNullOrWhiteSpace(StoreType) ? null : StoreType.Trim(),
            Keeper = Keeper?.ToReference(),
            TemperatureControlled = TemperatureControlled ?? false,
            RestrictedAccess = RestrictedAccess ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateStoreRequest(
    string? StoreName,
    string? Code,
    MasterDataReferenceDto? Warehouse,
    string? Description,
    string? StoreType,
    MasterDataReferenceDto? Keeper,
    bool? TemperatureControlled,
    bool? RestrictedAccess,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(StoreEntity store)
    {
        store.StoreName = StoreName!.Trim();
        store.Code = Code!.Trim();
        store.Warehouse = Warehouse?.ToReference();
        store.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        store.StoreType = string.IsNullOrWhiteSpace(StoreType) ? null : StoreType.Trim();
        store.Keeper = Keeper?.ToReference();
        store.TemperatureControlled = TemperatureControlled ?? false;
        store.RestrictedAccess = RestrictedAccess ?? false;

        store.UpdatedBy = UpdatedBy;
        store.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            store.Status = Status;
        }
    }
}

public sealed record UpdateStoreStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record StoreResponse(
    string Id,
    string StoreName,
    string Code,
    MasterDataReferenceDto? Warehouse,
    string? Description,
    string? StoreType,
    MasterDataReferenceDto? Keeper,
    bool TemperatureControlled,
    bool RestrictedAccess,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static StoreResponse FromEntity(StoreEntity store)
    {
        return new StoreResponse(
            store.Id ?? string.Empty,
            store.StoreName ?? string.Empty,
            store.Code ?? string.Empty,
            store.Warehouse is null
                ? null
                : MasterDataReferenceDto.FromReference(store.Warehouse),
            store.Description,
            store.StoreType,
            store.Keeper is null
                ? null
                : MasterDataReferenceDto.FromReference(store.Keeper),
            store.TemperatureControlled,
            store.RestrictedAccess,
            store.CreatedBy,
            store.CreatedAt,
            store.UpdatedBy,
            store.UpdatedAt,
            store.ClientId,
            store.TenantId,
            store.Status,
            store.IsDeleted
        );
    }
}
