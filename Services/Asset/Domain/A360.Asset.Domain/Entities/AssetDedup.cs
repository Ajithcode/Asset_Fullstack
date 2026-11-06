using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>Duplicate-detection rules for the Asset Merge / Dedup screen. One record holds the current rules.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetDedupRules : BaseEntity
{
    /// <summary>Fields compared: name, serialNumber, model, manufacturer, category, location.</summary>
    [BsonElement("fields")]
    public List<string> Fields { get; set; } = [];

    /// <summary>Lowest match score (0-100) for each confidence band.</summary>
    [BsonElement("high")]
    public int High { get; set; }

    [BsonElement("medium")]
    public int Medium { get; set; }

    [BsonElement("low")]
    public int Low { get; set; }

    /// <summary>Also compare disposed assets.</summary>
    [BsonElement("include_inactive")]
    public bool IncludeInactive { get; set; }

    [BsonElement("last_scan_at")]
    public DateTime? LastScanAt { get; set; }
}

/// <summary>Two assets marked "not a duplicate", so they are no longer matched with each other.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetDedupDismissal : BaseEntity
{
    /// <summary>The two asset tags, sorted.</summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>"TAG-A|TAG-B" (sorted), unique per pair.</summary>
    [BsonElement("pair_key")]
    public string PairKey { get; set; } = string.Empty;

    [BsonElement("reason")]
    public string? Reason { get; set; }
}

/// <summary>A completed merge: the duplicates folded into the primary asset, with the values it kept.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetMerge : BaseEntity
{
    [BsonElement("primary_tag")]
    public string PrimaryTag { get; set; } = string.Empty;

    [BsonElement("primary_asset_registration_id")]
    public string PrimaryAssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("merged_tags")]
    public List<string> MergedTags { get; set; } = [];

    [BsonElement("merged_asset_registration_ids")]
    public List<string> MergedAssetRegistrationIds { get; set; } = [];

    /// <summary>Merge field → tag of the record the value was taken from.</summary>
    [BsonElement("field_sources")]
    public Dictionary<string, string> FieldSources { get; set; } = [];

    /// <summary>Merge field → value the primary asset kept (as text).</summary>
    [BsonElement("final_values")]
    public Dictionary<string, string> FinalValues { get; set; } = [];

    [BsonElement("notes")]
    public string? Notes { get; set; }
}
