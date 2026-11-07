using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An asset category; categories nest through ParentCategoryId (null for a top-level category).</summary>
/// <remarks>Status (Active/Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetCategory : BaseEntity
{
    [BsonElement("parent_category_id")]
    public string? ParentCategoryId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // URL returned by the Media service upload endpoint.
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    // Defined on this category; sub-categories inherit them.
    [BsonElement("custom_fields")]
    public List<AssetCategoryCustomField> CustomFields { get; set; } = [];

    [BsonElement("attributes")]
    public List<AssetCategoryAttribute> Attributes { get; set; } = [];

    // Master Data Management's settings for the category (asset type, look, defaults for new assets...).
    // Null for categories made on the Assets page; that page never sends it, so its saves keep what is here.
    [BsonElement("details")]
    public AssetCategoryDetails? Details { get; set; }
}

/// <summary>A field assets in this category fill in during registration (e.g. "Installation Date", Date, required).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCategoryCustomField
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("field_name")]
    public string FieldName { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("required")]
    public bool Required { get; set; }

    [BsonElement("created_by")]
    public string? CreatedBy { get; set; }

    [BsonElement("created_at")]
    public DateTime? CreatedAt { get; set; }
}

/// <summary>A technical attribute assets in this category have (e.g. "Power Rating", kVA, 250).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCategoryAttribute
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("attribute")]
    public string Attribute { get; set; } = string.Empty;

    [BsonElement("unit")]
    public string? Unit { get; set; }

    [BsonElement("example_value")]
    public string? ExampleValue { get; set; }

    [BsonElement("created_by")]
    public string? CreatedBy { get; set; }

    [BsonElement("created_at")]
    public DateTime? CreatedAt { get; set; }
}

/// <summary>Category settings kept for Master Data Management › Category &amp; Sub-category.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCategoryDetails
{
    // How assets in the category are accounted for, e.g. Fixed Asset or Consumable
    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("subcategories")]
    public List<AssetCategorySubcategory> Subcategories { get; set; } = [];

    // Defaults for new assets in the category
    [BsonElement("depreciation_method")]
    public string? DepreciationMethod { get; set; }

    [BsonElement("useful_life_years")]
    public double? UsefulLifeYears { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("tag_prefix")]
    public string? TagPrefix { get; set; }

    [BsonElement("tracking_method")]
    public string? TrackingMethod { get; set; }

    [BsonElement("default_condition")]
    public string? DefaultCondition { get; set; }

    [BsonElement("subcategory_required")]
    public bool SubcategoryRequired { get; set; }

    [BsonElement("show_in_mobile")]
    public bool ShowInMobile { get; set; }

    [BsonElement("include_in_reports")]
    public bool IncludeInReports { get; set; }

    [BsonElement("approval_for_new_assets")]
    public bool ApprovalForNewAssets { get; set; }
}

/// <summary>A sub-category listed inside its category on the Master Data page.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCategorySubcategory
{
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }
}
