using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// A saved combination of layers switched on, e.g. "Maintenance View".
[BsonIgnoreExtraElements]
public sealed class MapLayerSet : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("layer_set_id")]
    public string LayerSetId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Layer key -> on/off.
    [BsonElement("layers")]
    public Dictionary<string, bool> Layers { get; set; } = [];
}
