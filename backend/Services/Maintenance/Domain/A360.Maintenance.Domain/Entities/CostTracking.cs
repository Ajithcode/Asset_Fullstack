
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class CostTracking : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("labor_cost")]
    public double LaborCost { get; set; }

    [BsonElement("spare_parts_cost")]
    public double SparePartsCost { get; set; }

    [BsonElement("total_maintenance_cost")]
    public double TotalMaintenanceCost { get; set; }

    [BsonElement("budget_allocation")]
    public double BudgetAllocation { get; set; }

    [BsonElement("cost_per_asset")]
    public double CostPerAsset { get; set; }
}
