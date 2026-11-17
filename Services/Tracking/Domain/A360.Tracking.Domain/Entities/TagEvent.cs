using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// One row of the Tag Management Audit Trail: a registration, assignment, read or status change of a tag, with who made it.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class TagEvent : BaseEntity
{
    [BsonElement("tag_id")]
    public string TagId { get; set; } = string.Empty;

    [BsonElement("technology")]
    public string Technology { get; set; } = string.Empty;

    /// <summary>One of <see cref="TagEventType.All"/>.</summary>
    [BsonElement("event_type")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>e.g. "Assigned to Chiller CH-011", "Last read by Gateway GW-DXB-04".</summary>
    [BsonElement("details")]
    public string Details { get; set; } = string.Empty;

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_code")]
    public string? AssetCode { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    /// <summary>The tag's status after the event, so the status on any past day can be worked out; null once deleted.</summary>
    [BsonElement("status_after")]
    public string? StatusAfter { get; set; }

    /// <summary>The user name, or "System" for reads.</summary>
    [BsonElement("performed_by")]
    public string PerformedBy { get; set; } = string.Empty;

    [BsonElement("occurred_at")]
    public DateTime OccurredAt { get; set; }
}

public static class TagEventType
{
    public const string Registered = "Registered";
    public const string Assigned = "Assigned";
    public const string Reassigned = "Reassigned";
    public const string Unassigned = "Unassigned";
    public const string Updated = "Updated";
    public const string Read = "Read";
    public const string Deactivated = "Deactivated";
    public const string Reactivated = "Reactivated";
    public const string Deleted = "Deleted";

    public static readonly IReadOnlyList<string> All =
        [Registered, Assigned, Reassigned, Unassigned, Updated, Read, Deactivated, Reactivated, Deleted];

    /// <summary>The events shown in a tag's Assignment History.</summary>
    public static readonly IReadOnlyList<string> Assignment = [Assigned, Reassigned, Unassigned];

    public const string SystemActor = "System";
}
