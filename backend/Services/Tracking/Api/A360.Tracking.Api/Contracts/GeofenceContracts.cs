using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Adds a row to the Geofence List: Name, Type, Site, Monitored Assets, Rules, Status, Last Event.</summary>
/// <param name="GeofenceType">One of <see cref="Domain.Entities.GeofenceType.All"/>.</param>
/// <param name="Status">One of <see cref="GeofenceStatus.All"/>; defaults to Active.</param>
public sealed record CreateGeofenceRequest(
    string? Name,
    string? GeofenceType,
    string? SiteId,
    string? SiteName,
    int? MonitoredAssetCount,
    int? RuleCount,
    string? Status,
    DateTime? LastEventAt,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public Geofence ToEntity(CurrentUser? currentUser)
    {
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
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

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
            geofence.CreatedBy,
            geofence.CreatedAt,
            geofence.ClientId,
            geofence.TenantId);
    }
}
