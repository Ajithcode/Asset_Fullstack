using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetModel : BaseEntity
{
    [BsonElement("model_name")]
    public string ModelName { get; set; } = null!;

    [BsonElement("model_code")]
    public string ModelCode { get; set; } = null!;

    // Manufacturer, asset type and category all point at records in this service.
    [BsonElement("manufacturer")]
    public MasterDataReference? Manufacturer { get; set; }

    [BsonElement("asset_type")]
    public MasterDataReference? AssetType { get; set; }

    [BsonElement("category")]
    public MasterDataReference? Category { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("lifecycle")]
    public string? Lifecycle { get; set; }

    // Uploaded picture as a data URL; the asset type icon is shown when empty.
    [BsonElement("image")]
    public string? Image { get; set; }

    [BsonElement("specs")]
    public List<AssetModelSpec> Specs { get; set; } = [];

    [BsonElement("lifecycle_years")]
    public int? LifecycleYears { get; set; }

    [BsonElement("warranty_months")]
    public int? WarrantyMonths { get; set; }

    [BsonElement("pm_frequency")]
    public string? PmFrequency { get; set; }

    [BsonElement("maintenance_strategy")]
    public string? MaintenanceStrategy { get; set; }

    [BsonElement("criticality")]
    public string? Criticality { get; set; }

    [BsonElement("calibration_required")]
    public bool CalibrationRequired { get; set; }

    // Financial defaults copied to new assets of this model.
    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("purchase_price")]
    public decimal? PurchasePrice { get; set; }

    [BsonElement("include_in_depreciation")]
    public bool IncludeInDepreciation { get; set; }

    [BsonElement("depreciation_method")]
    public string? DepreciationMethod { get; set; }

    [BsonElement("salvage_percent")]
    public decimal? SalvagePercent { get; set; }

    // CAPEX or OPEX.
    [BsonElement("procurement_type")]
    public string? ProcurementType { get; set; }

    // Defaults for new assets.
    [BsonElement("subcategories")]
    public List<string> Subcategories { get; set; } = [];

    [BsonElement("tagging_format")]
    public string? TaggingFormat { get; set; }

    [BsonElement("tracking_method")]
    public string? TrackingMethod { get; set; }

    [BsonElement("default_condition")]
    public string? DefaultCondition { get; set; }

    [BsonElement("serial_required")]
    public bool SerialRequired { get; set; }

    [BsonElement("assignable")]
    public bool Assignable { get; set; }
}

// One line of the model's specification sheet, e.g. "Processor" = "Intel Core i7".
public class AssetModelSpec
{
    [BsonElement("label")]
    public string Label { get; set; } = null!;

    [BsonElement("value")]
    public string? Value { get; set; }
}
