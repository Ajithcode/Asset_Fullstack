using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>
/// Lifecycle page of one registered asset: stage changes and manual events. Everything else on the page
/// (purchase, assignments, condition assessments, service requests, disposal) is read from its own records.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AssetLifecycleRecord : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("stages")]
    public List<AssetLifecycleStage> Stages { get; set; } = [];

    [BsonElement("events")]
    public List<AssetLifecycleEvent> Events { get; set; } = [];
}

/// <summary>A stage as last set by a user; stages never set are worked out from the asset's records.</summary>
public sealed class AssetLifecycleStage
{
    // planning, procurement, received, installation, commissioning, operation, maintenance, upgrade, decommissioning, disposal
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    // "Completed", "Current", "Planned" or "Not Started".
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("date")]
    public DateTime? Date { get; set; }

    [BsonElement("owner")]
    public string? Owner { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("updated_by")]
    public string? UpdatedBy { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class AssetLifecycleEvent
{
    [BsonElement("event_id")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("by")]
    public string? By { get; set; }

    // Procurement, Installation, Operation, Maintenance, Repair, Condition, Stage or Other.
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
