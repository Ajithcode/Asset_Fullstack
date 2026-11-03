using WarehouseEntity = A360.MasterDataManagement.Domain.Entities.Warehouse;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateWarehouseRequest(
    string? WarehouseName,
    string? Code,
    string? Location,
    MasterDataReferenceDto? Manager,
    string? Description,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Email,
    double? AreaSqm,
    string? OperatingHours,
    bool? AllowTransfersOut,
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
            AddressLine1 = string.IsNullOrWhiteSpace(AddressLine1) ? null : AddressLine1.Trim(),
            City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
            State = string.IsNullOrWhiteSpace(State) ? null : State.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(PostalCode) ? null : PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim().ToLowerInvariant(),
            AreaSqm = AreaSqm,
            OperatingHours = string.IsNullOrWhiteSpace(OperatingHours) ? null : OperatingHours.Trim(),
            AllowTransfersOut = AllowTransfersOut ?? true,

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
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Email,
    double? AreaSqm,
    string? OperatingHours,
    bool? AllowTransfersOut,
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
        warehouse.AddressLine1 = string.IsNullOrWhiteSpace(AddressLine1) ? null : AddressLine1.Trim();
        warehouse.City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
        warehouse.State = string.IsNullOrWhiteSpace(State) ? null : State.Trim();
        warehouse.PostalCode = string.IsNullOrWhiteSpace(PostalCode) ? null : PostalCode.Trim();
        warehouse.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        warehouse.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        warehouse.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim().ToLowerInvariant();
        warehouse.AreaSqm = AreaSqm;
        warehouse.OperatingHours = string.IsNullOrWhiteSpace(OperatingHours) ? null : OperatingHours.Trim();
        warehouse.AllowTransfersOut = AllowTransfersOut ?? true;

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
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Email,
    double? AreaSqm,
    string? OperatingHours,
    bool AllowTransfersOut,
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
            warehouse.AddressLine1,
            warehouse.City,
            warehouse.State,
            warehouse.PostalCode,
            warehouse.Country,
            warehouse.Phone,
            warehouse.Email,
            warehouse.AreaSqm,
            warehouse.OperatingHours,
            warehouse.AllowTransfersOut,
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
