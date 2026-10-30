using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateLocationRequest(
    string? LocationCode,
    string? LocationName,
    string? LocationType,
    string? ParentLocationId,
    string? Address,
    string? Country,
    string? City,
    string? TimeZone,
    string? LocationManager,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

// LocationId, audit and tenant/client fields are fixed once the location is created.
public sealed record UpdateLocationRequest(
    string? LocationCode,
    string? LocationName,
    string? LocationType,
    string? ParentLocationId,
    string? Address,
    string? Country,
    string? City,
    string? TimeZone,
    string? LocationManager,
    string? Status);

public sealed record ParentLocationResponse(
    string LocationId,
    string LocationCode,
    string LocationName);

public sealed record LocationResponse(
    string LocationId,
    string LocationCode,
    string LocationName,
    string LocationType,
    ParentLocationResponse? ParentLocation,
    string? Address,
    string Country,
    string City,
    string TimeZone,
    string LocationManager,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static LocationResponse FromEntity(Location location, Location? parent)
    {
        return new LocationResponse(
            location.LocationId,
            location.LocationCode,
            location.LocationName,
            location.LocationType,
            parent is null ? null : new ParentLocationResponse(parent.LocationId, parent.LocationCode, parent.LocationName),
            location.Address,
            location.Country,
            location.City,
            location.TimeZone,
            location.LocationManager,
            location.Status,
            location.CreatedBy,
            location.CreatedAt,
            location.UpdatedBy,
            location.UpdatedAt,
            location.TenantId,
            location.ClientId);
    }
}
