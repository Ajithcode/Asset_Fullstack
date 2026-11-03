
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class KpiMaster : BaseEntity
{
    [BsonElement("kpi_id")]
    public string KpiId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("kpi_name")]
    public string KpiName { get; set; } = null!;

    [BsonElement("formula_definition")]
    public string? FormulaDefinition { get; set; }

    [BsonElement("threshold_green")]
    public double? ThresholdGreen { get; set; }

    [BsonElement("threshold_amber")]
    public double? ThresholdAmber { get; set; }

    [BsonElement("threshold_red")]
    public double? ThresholdRed { get; set; }

    [BsonElement("refresh_frequency")]
    public string? RefreshFrequency { get; set; }

    [BsonElement("widget_type")]
    public string? WidgetType { get; set; }
}
