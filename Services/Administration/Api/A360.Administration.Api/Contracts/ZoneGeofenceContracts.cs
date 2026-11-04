using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateZoneGeofenceRequest(
    string? FloorPlanId,
    string? FloorPlanName,
    string? ZoneName,
    string? ZoneCode,
    string? ZoneType,
    string? LocationId,
    string? LocationPath,
    double? Area,
    double? Perimeter,
    string? Description,
    string? AccessRule,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record ZoneGeofenceResponse(
    string ZoneId,
    string? FloorPlanId,
    string? FloorPlanName,
    string ZoneName,
    string ZoneCode,
    string ZoneType,
    string? LocationId,
    string? LocationPath,
    double? Area,
    double? Perimeter,
    string? Description,
    string? AccessRule,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static ZoneGeofenceResponse FromEntity(ZoneGeofence zone)
    {
        return new ZoneGeofenceResponse(
            zone.ZoneId,
            zone.FloorPlanId,
            zone.FloorPlanName,
            zone.ZoneName,
            zone.ZoneCode,
            zone.ZoneType,
            zone.LocationId,
            zone.LocationPath,
            zone.Area,
            zone.Perimeter,
            zone.Description,
            zone.AccessRule,
            zone.Status,
            zone.CreatedBy,
            zone.CreatedAt,
            zone.UpdatedBy,
            zone.UpdatedAt,
            zone.TenantId,
            zone.ClientId);
    }
}

/// <summary>Choices for the Zone Type and Access Rule dropdowns.</summary>
public sealed record ZoneGeofenceOptionsResponse(
    IReadOnlyList<string> ZoneTypes,
    IReadOnlyList<string> AccessRules,
    IReadOnlyList<string> Statuses)
{
    public static readonly ZoneGeofenceOptionsResponse Instance =
        new(MapZoneType.All, MapZoneAccessRule.All, MapZoneStatus.All);
}
