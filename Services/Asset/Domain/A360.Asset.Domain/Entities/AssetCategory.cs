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
