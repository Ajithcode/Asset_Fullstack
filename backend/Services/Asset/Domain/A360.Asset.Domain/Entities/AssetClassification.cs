using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Domain.Entities;

/// <summary>Step 2 of asset registration: the asset's category, type and key classification details.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetClassification
{
    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("sub_category")]
    public string? SubCategory { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("asset_sub_type")]
    public string? AssetSubType { get; set; }

    [BsonElement("function_purpose")]
    public string? FunctionPurpose { get; set; }

    [BsonElement("criticality")]
    public string? Criticality { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("business_unit")]
    public string? BusinessUnit { get; set; }

    [BsonElement("tagging_method")]
    public string? TaggingMethod { get; set; }

    [BsonElement("compliance_category")]
    public string? ComplianceCategory { get; set; }

    [BsonElement("lifecycle_stage")]
    public string? LifecycleStage { get; set; }

    [BsonElement("keywords")]
    public List<string> Keywords { get; set; } = [];
}
