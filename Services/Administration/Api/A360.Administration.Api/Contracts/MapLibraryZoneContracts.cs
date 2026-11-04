using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateMapLibraryZoneRequest(
    string? FloorPlanId,
    string? FloorPlanName,
    string? ZoneName,
    string? ZoneCode,
    string? ZoneType,
    string? ParentLocationId,
    string? ParentLocationPath,
    double? Area,
    string? Description,
    string? AccessRule,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record MapLibraryZoneResponse(
    string ZoneId,
    string? FloorPlanId,
    string? FloorPlanName,
    string ZoneName,
    string ZoneCode,
    string ZoneType,
    string? ParentLocationId,
    string? ParentLocationPath,
    double? Area,
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
    public static MapLibraryZoneResponse FromEntity(MapLibraryZone zone)
    {
        return new MapLibraryZoneResponse(
            zone.ZoneId,
            zone.FloorPlanId,
            zone.FloorPlanName,
            zone.ZoneName,
            zone.ZoneCode,
            zone.ZoneType,
            zone.ParentLocationId,
            zone.ParentLocationPath,
            zone.Area,
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
public sealed record MapLibraryZoneOptionsResponse(
    IReadOnlyList<string> ZoneTypes,
    IReadOnlyList<string> AccessRules,
    IReadOnlyList<string> Statuses)
{
    public static readonly MapLibraryZoneOptionsResponse Instance =
        new(MapZoneType.All, MapZoneAccessRule.All, MapZoneStatus.All);
}
