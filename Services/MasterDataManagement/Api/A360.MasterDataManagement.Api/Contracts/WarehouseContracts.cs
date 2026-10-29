using WarehouseEntity = A360.MasterDataManagement.Domain.Entities.Warehouse;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateWarehouseRequest(
    string? WarehouseName,
    string? Code,
    string? Location,
    MasterDataReferenceDto? Manager,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public WarehouseEntity ToEntity()
    {
        return new WarehouseEntity
        {
            WarehouseName = WarehouseName!.Trim(),
            Code = Code!.Trim(),
            Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
            Manager = Manager?.ToReference(),
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

public sealed record UpdateWarehouseRequest(
    string? WarehouseName,
    string? Code,
    string? Location,
    MasterDataReferenceDto? Manager,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(WarehouseEntity warehouse)
    {
        warehouse.WarehouseName = WarehouseName!.Trim();
        warehouse.Code = Code!.Trim();
        warehouse.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        warehouse.Manager = Manager?.ToReference();
        warehouse.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        warehouse.UpdatedBy = UpdatedBy;
        warehouse.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            warehouse.Status = Status;
        }
    }
}

public sealed record UpdateWarehouseStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record WarehouseResponse(
    string Id,
    string WarehouseName,
    string Code,
    string? Location,
    MasterDataReferenceDto? Manager,
    string? Description,
    int StoreCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static WarehouseResponse FromEntity(
        WarehouseEntity warehouse,
        int storeCount)
    {
        return new WarehouseResponse(
            warehouse.Id ?? string.Empty,
            warehouse.WarehouseName ?? string.Empty,
            warehouse.Code ?? string.Empty,
            warehouse.Location,
            warehouse.Manager is null
                ? null
                : MasterDataReferenceDto.FromReference(warehouse.Manager),
            warehouse.Description,
            storeCount,
            warehouse.CreatedBy,
            warehouse.CreatedAt,
            warehouse.UpdatedBy,
            warehouse.UpdatedAt,
            warehouse.ClientId,
            warehouse.TenantId,
            warehouse.Status,
            warehouse.IsDeleted
        );
    }
}
