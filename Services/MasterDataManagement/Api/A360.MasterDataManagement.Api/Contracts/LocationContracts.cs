using LocationEntity = A360.MasterDataManagement.Domain.Entities.Location;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateLocationRequest(
    string? LocationName,
    string? Code,
    string? Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public LocationEntity ToEntity()
    {
        return new LocationEntity
        {
            LocationName = LocationName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Building = Building,
            Floor = Floor,
            Zone = Zone,
            ParentLocationId = string.IsNullOrWhiteSpace(ParentLocationId) ? null : ParentLocationId,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateLocationRequest(
    string? LocationName,
    string? Code,
    string? Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(LocationEntity location)
    {
        location.LocationName = LocationName!.Trim();
        location.Code = Code!.Trim();
        location.Type = Type!.Trim();
        location.Building = Building;
        location.Floor = Floor;
        location.Zone = Zone;
        location.ParentLocationId = string.IsNullOrWhiteSpace(ParentLocationId) ? null : ParentLocationId;

        location.UpdatedBy = UpdatedBy;
        location.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            location.Status = Status;
        }
    }
}

public sealed record UpdateLocationStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record LocationResponse(
    string Id,
    string LocationName,
    string Code,
    string Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? ParentLocationName,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static LocationResponse FromEntity(LocationEntity location)
    {
        return new LocationResponse(
            location.Id ?? string.Empty,
            location.LocationName ?? string.Empty,
            location.Code ?? string.Empty,
            location.Type ?? string.Empty,
            location.Building,
            location.Floor,
            location.Zone,
            location.ParentLocationId,
            location.ParentLocationName,
            location.CreatedBy,
            location.CreatedAt,
            location.UpdatedBy,
            location.UpdatedAt,
            location.ClientId,
            location.TenantId,
            location.Status,
            location.IsDeleted
        );
    }
}
