using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>
/// A node of an asset's Component Structure (e.g. "Drive Train" &gt; "Gearbox"): a named grouping that can nest
/// under another node and optionally point at one of the asset's components.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AssetComponentStructureNode : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("parent_node_id")]
    public string? ParentNodeId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Id of an AssetComponent of the same asset; null when the node is only a grouping.</summary>
    [BsonElement("linked_component_id")]
    public string? LinkedComponentId { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
