
using LocationMasterEntity = A360.Wip.Domain.Entities.LocationMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateLocationMasterRequest(
    string? LocationId,
    string? AssetId,
    string? AssetName,
    string? Site,
    string? Building,
    string? Floor,
    string? Zone,
    string? GeoCoordinates,
    string? ParentLocation,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public LocationMasterEntity ToEntity()
    {
        return new LocationMasterEntity
        {
            LocationId = LocationId,
            AssetId = AssetId,
            AssetName = AssetName,

            Site = Site,
            Building = Building,
            Floor = Floor,
            Zone = Zone,

            GeoCoordinates = GeoCoordinates,
            ParentLocation = ParentLocation,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateLocationMasterRequest(
    string? AssetId,
    string? AssetName,
    string? Site,
    string? Building,
    string? Floor,
    string? Zone,
    string? GeoCoordinates,
    string? ParentLocation,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(LocationMasterEntity locationMaster)
    {
        locationMaster.AssetId = AssetId;
        locationMaster.AssetName = AssetName;

        locationMaster.Site = Site;
        locationMaster.Building = Building;
        locationMaster.Floor = Floor;
        locationMaster.Zone = Zone;

        locationMaster.GeoCoordinates = GeoCoordinates;
        locationMaster.ParentLocation = ParentLocation;

        locationMaster.UpdatedBy = UpdatedBy;
        locationMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            locationMaster.Status = Status;
        }
    }
}

public sealed record LocationMasterResponse(
    string Id,
    string LocationId,
    string? AssetId,
    string? AssetName,
    string? Site,
    string? Building,
    string? Floor,
    string? Zone,
    string? GeoCoordinates,
    string? ParentLocation,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static LocationMasterResponse FromEntity(LocationMasterEntity locationMaster)
    {
        return new LocationMasterResponse(
            locationMaster.Id ?? string.Empty,
            locationMaster.LocationId ?? string.Empty,
            locationMaster.AssetId,
            locationMaster.AssetName,

            locationMaster.Site,
            locationMaster.Building,
            locationMaster.Floor,
            locationMaster.Zone,

            locationMaster.GeoCoordinates,
            locationMaster.ParentLocation,

            locationMaster.CreatedBy,
            locationMaster.CreatedAt,
            locationMaster.UpdatedBy,
            locationMaster.UpdatedAt,

            locationMaster.ClientId,
            locationMaster.TenantId,
            locationMaster.Status,
            locationMaster.IsDeleted
        );
    }
}
