using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Adds a row to Recent Location Events: Time, Asset ID, Asset Name, Event, Zone, Details. The EventId (e.g. LE000001) is generated.</summary>
/// <param name="EventTime">When the event happened; defaults to now.</param>
/// <param name="EventType">One of <see cref="LocationEventType.All"/>.</param>
public sealed record CreateLocationEventRequest(
    DateTime? EventTime,
    string? AssetId,
    string? AssetName,
    string? EventType,
    string? ZoneName,
    string? Details,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public LocationEvent ToEntity(string eventId, CurrentUser? currentUser)
    {
        return new LocationEvent
        {
            EventId = eventId,
            EventTime = EventTime?.ToUniversalTime() ?? DateTime.UtcNow,
            AssetId = AssetId!.Trim(),
            AssetName = AssetName!.Trim(),
            EventType = ContractText.Canonical(EventType, LocationEventType.All)!,
            ZoneName = ZoneName!.Trim(),
            Details = ContractText.TrimOrNull(Details),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record LocationEventResponse(
    string Id,
    string EventId,
    DateTime EventTime,
    string AssetId,
    string AssetName,
    string EventType,
    string ZoneName,
    string? Details,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static LocationEventResponse FromEntity(LocationEvent locationEvent)
    {
        return new LocationEventResponse(
            locationEvent.Id,
            locationEvent.EventId,
            locationEvent.EventTime,
            locationEvent.AssetId,
            locationEvent.AssetName,
            locationEvent.EventType,
            locationEvent.ZoneName,
            locationEvent.Details,
            locationEvent.CreatedBy,
            locationEvent.CreatedAt,
            locationEvent.ClientId,
            locationEvent.TenantId);
    }
}
