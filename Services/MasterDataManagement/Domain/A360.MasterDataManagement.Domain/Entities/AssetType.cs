using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetType : BaseEntity
{
    [BsonElement("asset_type_name")]
    public string AssetTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at an asset category in this service.
    [BsonElement("category")]
    public MasterDataReference? Category { get; set; }

    [BsonElement("asset_class")]
    public string? AssetClass { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("depreciation_method")]
    public string? DepreciationMethod { get; set; }

    // Lifecycle in years.
    [BsonElement("default_lifetime")]
    public int? DefaultLifetime { get; set; }

    // Built-in icon key, used when no image is uploaded.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    // Uploaded picture as a data URL.
    [BsonElement("image")]
    public string? Image { get; set; }

    // Names of subcategories of the category that this type covers.
    [BsonElement("subcategories")]
    public List<string> Subcategories { get; set; } = [];

    // Asset tag pattern, e.g. "LAP-{YY}-{0000}".
    [BsonElement("tagging_format")]
    public string? TaggingFormat { get; set; }

    [BsonElement("maintenance_type")]
    public string? MaintenanceType { get; set; }

    [BsonElement("maintenance_plan")]
    public string? MaintenancePlan { get; set; }

    [BsonElement("warranty_months")]
    public int? WarrantyMonths { get; set; }

    [BsonElement("service_provider")]
    public string? ServiceProvider { get; set; }

    // Depreciation period, in months.
    [BsonElement("useful_life_months")]
    public int? UsefulLifeMonths { get; set; }

    [BsonElement("residual_percent")]
    public decimal? ResidualPercent { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    // Assets bought for less than this are expensed, not capitalised.
    [BsonElement("capitalization_threshold")]
    public decimal? CapitalizationThreshold { get; set; }

    // CAPEX or OPEX.
    [BsonElement("procurement_type")]
    public string? ProcurementType { get; set; }

    [BsonElement("default_condition")]
    public string? DefaultCondition { get; set; }

    [BsonElement("tracking_method")]
    public string? TrackingMethod { get; set; }

    [BsonElement("serial_required")]
    public bool SerialRequired { get; set; }

    [BsonElement("assignable")]
    public bool Assignable { get; set; }

    [BsonElement("custom_fields")]
    public List<AssetCustomField> CustomFields { get; set; } = [];

    [BsonElement("inspection_required")]
    public bool InspectionRequired { get; set; }

    [BsonElement("allow_transfers")]
    public bool AllowTransfers { get; set; }

    [BsonElement("iot_tracking")]
    public bool IotTracking { get; set; }

    [BsonElement("disposal_approval")]
    public bool DisposalApproval { get; set; }
}
