using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Display options of one layer (zones, geofences, assets, ...) on a floor plan.
// Only layers someone changed are stored; the rest come from the built-in defaults.
[BsonIgnoreExtraElements]
public sealed class MapLayer : BaseEntity
{
    [BsonElement("floor_plan_id")]
    public string FloorPlanId { get; set; } = string.Empty;

    // zones, geofences, assets, devices, annotations, rooms, base or heatmap.
    [BsonElement("layer_key")]
    public string LayerKey { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("visible")]
    public bool Visible { get; set; }

    [BsonElement("locked")]
    public bool Locked { get; set; }

    [BsonElement("show_label")]
    public bool ShowLabel { get; set; }

    [BsonElement("show_id")]
    public bool ShowId { get; set; }

    [BsonElement("show_status_icon")]
    public bool ShowStatusIcon { get; set; }

    [BsonElement("use_category_color")]
    public bool UseCategoryColor { get; set; }

    [BsonElement("label_position")]
    public string? LabelPosition { get; set; }

    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    [BsonElement("status_filter")]
    public string? StatusFilter { get; set; }

    // 0-100.
    [BsonElement("opacity")]
    public int Opacity { get; set; }

    [BsonElement("color")]
    public string? Color { get; set; }
}
