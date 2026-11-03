using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>
/// A custom field definition. Scope says where it is used: on every asset, on asset components,
/// or only on assets of one asset category (and its sub-categories).
/// </summary>
/// <remarks>Status (Active/Inactive) is kept in BaseEntity.Status; inactive fields are hidden from forms but keep their data.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetCustomFieldDefinition : BaseEntity
{
    [BsonElement("scope")]
    public string Scope { get; set; } = string.Empty;

    // Lowercase with underscores, e.g. "warranty_expiry"; layouts refer to fields by this name.
    [BsonElement("field_name")]
    public string FieldName { get; set; } = string.Empty;

    [BsonElement("display_label")]
    public string DisplayLabel { get; set; } = string.Empty;

    // Cannot be changed once the field is created.
    [BsonElement("field_type")]
    public string FieldType { get; set; } = string.Empty;

    // The field group it belongs to (shown as "Category" on the field forms), and its position in that group.
    [BsonElement("group_id")]
    public string? GroupId { get; set; }

    [BsonElement("display_order")]
    public int DisplayOrder { get; set; }

    // Category scope only: the asset category whose assets get this field.
    [BsonElement("applies_to_category_id")]
    public string? AppliesToCategoryId { get; set; }

    // Component scope only: the kind of component it applies to, or "All Components".
    [BsonElement("component_type")]
    public string? ComponentType { get; set; }

    [BsonElement("required")]
    public bool Required { get; set; }

    [BsonElement("show_in_details")]
    public bool ShowInDetails { get; set; }

    [BsonElement("include_in_search")]
    public bool IncludeInSearch { get; set; }

    [BsonElement("default_value")]
    public string? DefaultValue { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("help_text")]
    public string? HelpText { get; set; }

    // Dropdown fields: the choices offered.
    [BsonElement("options")]
    public List<string> Options { get; set; } = [];

    // Compound fields: the parts entered together, e.g. Length, Width, Height.
    [BsonElement("compound_parts")]
    public List<AssetCustomFieldDefinitionPart> CompoundParts { get; set; } = [];

    // Lookup fields: the master list values come from, e.g. "suppliers".
    [BsonElement("lookup_source")]
    public string? LookupSource { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetCustomFieldDefinitionPart
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("label")]
    public string Label { get; set; } = string.Empty;

    [BsonElement("unit")]
    public string? Unit { get; set; }
}
