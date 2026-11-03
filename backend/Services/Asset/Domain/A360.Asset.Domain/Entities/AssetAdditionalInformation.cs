using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Domain.Entities;

/// <summary>Step 5 of asset registration: technical details, operational details, custom fields and attachments.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetAdditionalInformation
{
    [BsonElement("technical")]
    public AssetTechnicalDetails Technical { get; set; } = new();

    [BsonElement("operational")]
    public AssetOperationalDetails Operational { get; set; } = new();

    [BsonElement("custom_fields")]
    public List<AssetCustomField> CustomFields { get; set; } = [];

    // Manuals, certificates, photos and other files uploaded through the Media service.
    [BsonElement("attachments")]
    public List<AssetDocument> Attachments { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class AssetTechnicalDetails
{
    // Type-specific specifications (power rating, voltage, IP rating, ...) differ per asset type, so they are kept as a list.
    [BsonElement("specifications")]
    public List<AssetSpecification> Specifications { get; set; } = [];

    [BsonElement("dimension_length")]
    public decimal? DimensionLength { get; set; }

    [BsonElement("dimension_width")]
    public decimal? DimensionWidth { get; set; }

    [BsonElement("dimension_height")]
    public decimal? DimensionHeight { get; set; }

    [BsonElement("dimension_unit")]
    public string? DimensionUnit { get; set; }

    [BsonElement("weight")]
    public decimal? Weight { get; set; }

    [BsonElement("weight_unit")]
    public string? WeightUnit { get; set; }

    [BsonElement("expected_lifespan")]
    public int? ExpectedLifespan { get; set; }

    [BsonElement("expected_lifespan_unit")]
    public string? ExpectedLifespanUnit { get; set; }

    [BsonElement("technical_remarks")]
    public string? TechnicalRemarks { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetOperationalDetails
{
    [BsonElement("operating_hours_per_day")]
    public decimal? OperatingHoursPerDay { get; set; }

    [BsonElement("operating_hours_unit")]
    public string? OperatingHoursUnit { get; set; }

    [BsonElement("operating_shift")]
    public string? OperatingShift { get; set; }

    [BsonElement("utilization")]
    public string? Utilization { get; set; }

    [BsonElement("maintenance_frequency")]
    public int? MaintenanceFrequency { get; set; }

    [BsonElement("maintenance_frequency_unit")]
    public string? MaintenanceFrequencyUnit { get; set; }

    [BsonElement("last_maintenance_date")]
    public DateTime? LastMaintenanceDate { get; set; }

    [BsonElement("next_maintenance_date")]
    public DateTime? NextMaintenanceDate { get; set; }

    [BsonElement("compliance_standards")]
    public List<string> ComplianceStandards { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class AssetSpecification
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("value")]
    public string? Value { get; set; }

    [BsonElement("unit")]
    public string? Unit { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetCustomField
{
    [BsonElement("field_name")]
    public string FieldName { get; set; } = string.Empty;

    // How the value is entered on screen, e.g. Text, Number, Date, Dropdown.
    [BsonElement("field_type")]
    public string? FieldType { get; set; }

    [BsonElement("value")]
    public string? Value { get; set; }
}
