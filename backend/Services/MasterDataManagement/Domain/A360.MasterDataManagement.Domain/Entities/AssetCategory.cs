using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetCategory : BaseEntity
{
    [BsonElement("category_name")]
    public string CategoryName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Points at another asset category, so categories form a tree.
    [BsonElement("parent_category")]
    public MasterDataReference? ParentCategory { get; set; }

    // Assets live in the Asset service, so these are kept as typed and not checked here.
    [BsonElement("assets")]
    public List<MasterDataReference> Assets { get; set; } = [];

    // Built-in icon key, used when no image is uploaded.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("color")]
    public string? Color { get; set; }

    // Uploaded picture as a data URL.
    [BsonElement("image")]
    public string? Image { get; set; }

    // Subcategories belong to exactly one category, so they are kept inside it.
    [BsonElement("subcategories")]
    public List<AssetSubcategory> Subcategories { get; set; } = [];

    // Default values for new assets in this category.
    [BsonElement("depreciation_method")]
    public string? DepreciationMethod { get; set; }

    [BsonElement("useful_life_years")]
    public int? UsefulLifeYears { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("tag_prefix")]
    public string? TagPrefix { get; set; }

    [BsonElement("tracking_method")]
    public string? TrackingMethod { get; set; }

    [BsonElement("default_condition")]
    public string? DefaultCondition { get; set; }

    [BsonElement("custom_fields")]
    public List<AssetCustomField> CustomFields { get; set; } = [];

    [BsonElement("subcategory_required")]
    public bool SubcategoryRequired { get; set; }

    [BsonElement("show_in_mobile")]
    public bool ShowInMobile { get; set; } = true;

    [BsonElement("include_in_reports")]
    public bool IncludeInReports { get; set; } = true;

    [BsonElement("approval_for_new_assets")]
    public bool ApprovalForNewAssets { get; set; }
}

public class AssetSubcategory
{
    // Unique across all categories of the client.
    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }
}
