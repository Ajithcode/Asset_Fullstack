using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Adds a row to the Geofence List: Name, Type, Site, Monitored Assets, Rules, Status, Last Event.</summary>
/// <param name="GeofenceType">One of <see cref="Domain.Entities.GeofenceType.All"/>.</param>
/// <param name="Status">One of <see cref="GeofenceStatus.All"/>; defaults to Active.</param>
/// <param name="MapColour">One of <see cref="GeofenceMapColour.All"/>; defaults to blue.</param>
/// <param name="AssetGroups">At least one; "All assets" replaces any other group.</param>
/// <param name="Schedule">One of <see cref="GeofenceSchedule.All"/>; defaults to Always active.</param>
/// <param name="Shape">One of <see cref="GeofenceShape.All"/>; defaults to Polygon.</param>
/// <param name="PolygonPoints">Polygon corners; defaults to the number of coordinates, or 4 before the zone is drawn.</param>
/// <param name="Coordinates">Optional until the zone is drawn on the map; a circle takes just its centre.</param>
public sealed record CreateGeofenceRequest(
    string? Name,
    string? GeofenceType,
    string? SiteId,
    string? SiteName,
    int? MonitoredAssetCount,
    int? RuleCount,
    string? Status,
    DateTime? LastEventAt,
    string? MapColour,
    IReadOnlyList<string>? AssetGroups,
    string? Schedule,
    int? DwellAlertMinutes,
    bool? MonitorEntry,
    bool? MonitorExit,
    string? Shape,
    int? PolygonPoints,
    IReadOnlyList<GeofencePointRequest>? Coordinates,
    double? RadiusMeters,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public Geofence ToEntity(CurrentUser? currentUser)
    {
        var shape = ContractText.Canonical(Shape, GeofenceShape.All) ?? GeofenceShape.Polygon;
        var coordinates = (Coordinates ?? [])
            .Select(point => new GeofencePoint { Latitude = point.Latitude, Longitude = point.Longitude })
            .ToList();

        return new Geofence
        {
            Name = Name!.Trim(),
            GeofenceType = ContractText.Canonical(GeofenceType, Domain.Entities.GeofenceType.All)!,
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = SiteName!.Trim(),
            MonitoredAssetCount = MonitoredAssetCount ?? 0,
            RuleCount = RuleCount ?? 0,
            Status = ContractText.Canonical(Status, GeofenceStatus.All) ?? GeofenceStatus.Active,
            LastEventAt = LastEventAt?.ToUniversalTime(),
            MapColour = ContractText.Canonical(MapColour, GeofenceMapColour.All) ?? GeofenceMapColour.Blue,
            AssetGroups = NormalizedAssetGroups(),
            Schedule = ContractText.Canonical(Schedule, GeofenceSchedule.All) ?? GeofenceSchedule.AlwaysActive,
            DwellAlertMinutes = DwellAlertMinutes,
            MonitorEntry = MonitorEntry ?? true,
            MonitorExit = MonitorExit ?? true,
            Shape = shape,
            PolygonPoints = shape == GeofenceShape.Polygon
                ? PolygonPoints ?? (coordinates.Count > 0 ? coordinates.Count : GeofenceShape.DefaultPolygonPoints)
                : null,
            Coordinates = coordinates,
            RadiusMeters = shape == GeofenceShape.Circle ? RadiusMeters : null,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }

    /// <summary>Trims and de-duplicates the groups, using catalog spelling; "All assets" makes the others redundant.</summary>
    private List<string> NormalizedAssetGroups()
    {
        var groups = (AssetGroups ?? [])
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => ContractText.Canonical(group, GeofenceAssetGroup.All) ?? group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups.Contains(GeofenceAssetGroup.AllAssets) ? [GeofenceAssetGroup.AllAssets] : groups;
    }
}

public sealed record GeofencePointRequest(double Latitude, double Longitude);

public sealed record GeofencePointResponse(double Latitude, double Longitude);

public sealed record GeofenceResponse(
    string Id,
    string Name,
    string GeofenceType,
    string? SiteId,
    string SiteName,
    int MonitoredAssetCount,
    int RuleCount,
    string? Status,
    DateTime? LastEventAt,
    string MapColour,
    IReadOnlyList<string> AssetGroups,
    string Schedule,
    int? DwellAlertMinutes,
    bool MonitorEntry,
    bool MonitorExit,
    string Shape,
    int? PolygonPoints,
    IReadOnlyList<GeofencePointResponse> Coordinates,
    double? RadiusMeters,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static GeofenceResponse FromEntity(Geofence geofence)
    {
        return new GeofenceResponse(
            geofence.Id,
            geofence.Name,
            geofence.GeofenceType,
            geofence.SiteId,
            geofence.SiteName,
            geofence.MonitoredAssetCount,
            geofence.RuleCount,
            geofence.Status,
            geofence.LastEventAt,
            geofence.MapColour,
            geofence.AssetGroups,
            geofence.Schedule,
            geofence.DwellAlertMinutes,
            geofence.MonitorEntry,
            geofence.MonitorExit,
            geofence.Shape,
            geofence.PolygonPoints,
            geofence.Coordinates.Select(point => new GeofencePointResponse(point.Latitude, point.Longitude)).ToList(),
            geofence.RadiusMeters,
            geofence.CreatedBy,
            geofence.CreatedAt,
            geofence.ClientId,
            geofence.TenantId);
    }
}

/// <summary>Create Geofence form: every dropdown and picker in one call.</summary>
public sealed record GeofenceOptionsResponse(
    IReadOnlyList<string> ZoneTypes,
    IReadOnlyList<string> MapColours,
    IReadOnlyList<string> AssetGroups,
    IReadOnlyList<string> Schedules,
    IReadOnlyList<string> Shapes,
    IReadOnlyList<string> Statuses,
    int DefaultPolygonPoints,
    int MinPolygonPoints,
    int MaxPolygonPoints);
