using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A component of a registered asset (e.g. "Air Filter" in the "Engine Assembly" of a generator).</summary>
/// <remarks>Components nest through ParentComponentId within the same asset; Status is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetComponent : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("parent_component_id")]
    public string? ParentComponentId { get; set; }

    [BsonElement("component_name")]
    public string ComponentName { get; set; } = string.Empty;

    [BsonElement("component_tag")]
    public string ComponentTag { get; set; } = string.Empty;

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("part_number")]
    public string? PartNumber { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("installed_date")]
    public DateTime? InstalledDate { get; set; }

    [BsonElement("expected_replacement_date")]
    public DateTime? ExpectedReplacementDate { get; set; }

    // Life cycle: usage so far against the rated life, e.g. 4,000 of 10,000 running hours. A rated life of 0 means not tracked.
    [BsonElement("lifecycle_used")]
    public decimal LifecycleUsed { get; set; }

    [BsonElement("lifecycle_rated_life")]
    public decimal LifecycleRatedLife { get; set; }

    [BsonElement("lifecycle_unit")]
    public string? LifecycleUnit { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
