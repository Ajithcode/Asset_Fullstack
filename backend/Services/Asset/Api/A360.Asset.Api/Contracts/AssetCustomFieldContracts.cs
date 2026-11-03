using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>Where a custom field is used: the Asset Fields, Component Fields and Category Specific Fields tabs.</summary>
public static class CustomFieldScopes
{
    public const string Asset = "Asset";
    public const string Component = "Component";
    public const string Category = "Category";

    public static IEnumerable<string> All => [Asset, Component, Category];

    /// <summary>Asset and category fields both appear on asset forms, so their names must not clash with each other.</summary>
    public static IReadOnlyCollection<string> SameNamespaceAs(string scope) =>
        scope == Component ? [Component] : [Asset, Category];

    /// <summary>Returns the scope spelled as listed (e.g. "asset" → "Asset"), or null if it is not listed.</summary>
    public static string? Normalize(string? scope) =>
        All.FirstOrDefault(listed => string.Equals(listed, scope?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class CustomFieldTypes
{
    public const string Text = "Text";
    public const string TextArea = "TextArea";
    public const string Number = "Number";
    public const string Date = "Date";
    public const string Dropdown = "Dropdown";
    public const string Checkbox = "Checkbox";
    public const string Compound = "Compound";
    public const string Lookup = "Lookup";

    public static IEnumerable<string> All => [Text, TextArea, Number, Date, Dropdown, Checkbox, Compound, Lookup];

    /// <summary>Returns the type spelled as listed (e.g. "date" → "Date"), or null if it is not listed.</summary>
    public static string? Normalize(string? type) =>
        All.FirstOrDefault(listed => string.Equals(listed, type?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Standard asset fields that layouts can place alongside custom fields; custom fields cannot reuse these names.</summary>
public static class BuiltInAssetFields
{
    public static readonly IReadOnlyList<AvailableLayoutFieldResponse> All =
    [
        new("asset_name", "Asset Name", CustomFieldTypes.Text, true, AvailableLayoutFieldResponse.BuiltIn),
        new("asset_tag", "Asset Tag / ID", CustomFieldTypes.Text, true, AvailableLayoutFieldResponse.BuiltIn),
        new("barcode", "Barcode / QR Code", CustomFieldTypes.Text, false, AvailableLayoutFieldResponse.BuiltIn),
        new("rfid_tag_id", "RFID Tag ID", CustomFieldTypes.Text, false, AvailableLayoutFieldResponse.BuiltIn),
        new("model", "Model", CustomFieldTypes.Text, false, AvailableLayoutFieldResponse.BuiltIn),
        new("manufacturer", "Manufacturer", CustomFieldTypes.Text, false, AvailableLayoutFieldResponse.BuiltIn)
    ];

    public static bool Contains(string fieldName) => All.Any(field => field.FieldName == fieldName);
}

public sealed record CustomFieldPartDto(string? Key, string? Label, string? Unit);

/// <summary>Used for both create and update. The field type cannot change after the field is created.</summary>
public sealed record SaveAssetCustomFieldRequest(
    string? Scope,
    string? FieldName,
    string? DisplayLabel,
    string? FieldType,
    string? GroupId,
    string? AppliesToCategoryId,
    string? ComponentType,
    bool? Required,
    bool? ShowInDetails,
    bool? IncludeInSearch,
    string? Status,
    string? DefaultValue,
    string? Description,
    string? HelpText,
    List<string>? Options,
    List<CustomFieldPartDto>? CompoundParts,
    string? LookupSource,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public const string AllComponents = "All Components";

    public AssetCustomFieldDefinition ToEntity(CurrentUser? currentUser)
    {
        var field = new AssetCustomFieldDefinition
        {
            Scope = CustomFieldScopes.Normalize(Scope)!,
            FieldType = CustomFieldTypes.Normalize(FieldType)!,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        CopyFieldsTo(field);
        return field;
    }

    /// <remarks>Scope and field type are kept as created.</remarks>
    public void ApplyTo(AssetCustomFieldDefinition field, CurrentUser? currentUser)
    {
        CopyFieldsTo(field);
        field.UpdatedBy = currentUser?.UserName;
        field.UpdatedAt = DateTime.UtcNow;
    }

    private void CopyFieldsTo(AssetCustomFieldDefinition field)
    {
        field.FieldName = FieldName!.Trim();
        field.DisplayLabel = DisplayLabel!.Trim();
        field.GroupId = ContractText.TrimOrNull(GroupId);

        // Only the setting that belongs to the field's scope is kept.
        field.AppliesToCategoryId = field.Scope == CustomFieldScopes.Category ? ContractText.TrimOrNull(AppliesToCategoryId) : null;
        field.ComponentType = field.Scope == CustomFieldScopes.Component ? ContractText.TrimOrNull(ComponentType) ?? AllComponents : null;

        field.Required = Required ?? false;
        field.ShowInDetails = ShowInDetails ?? true;
        field.IncludeInSearch = IncludeInSearch ?? false;
        field.Status = ActiveInactiveStatuses.Normalize(Status);
        field.DefaultValue = ContractText.TrimOrNull(DefaultValue);
        field.Description = ContractText.TrimOrNull(Description);
        field.HelpText = ContractText.TrimOrNull(HelpText);

        // Type-specific settings are kept only for the type they belong to.
        field.Options = field.FieldType == CustomFieldTypes.Dropdown
            ? ContractText.CleanList(Options, StringComparer.OrdinalIgnoreCase)
            : [];
        field.CompoundParts = field.FieldType == CustomFieldTypes.Compound
            ? (CompoundParts ?? [])
                .Where(part => !string.IsNullOrWhiteSpace(part?.Key))
                .Select(part => new AssetCustomFieldDefinitionPart
                {
                    Key = part.Key!.Trim(),
                    Label = ContractText.TrimOrNull(part.Label) ?? part.Key!.Trim(),
                    Unit = ContractText.TrimOrNull(part.Unit)
                })
                .ToList()
            : [];
        field.LookupSource = field.FieldType == CustomFieldTypes.Lookup ? ContractText.TrimOrNull(LookupSource) : null;
    }
}

public sealed record AssetCustomFieldResponse(
    string Id,
    string Scope,
    string FieldName,
    string DisplayLabel,
    string FieldType,
    string? GroupId,
    string? GroupName,
    int DisplayOrder,
    string? AppliesToCategoryId,
    string? AppliesToCategoryName,
    string? ComponentType,
    bool Required,
    bool ShowInDetails,
    bool IncludeInSearch,
    string? Status,
    string? DefaultValue,
    string? Description,
    string? HelpText,
    IReadOnlyList<string> Options,
    IReadOnlyList<CustomFieldPartDto> CompoundParts,
    string? LookupSource,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetCustomFieldResponse FromEntity(AssetCustomFieldDefinition field, string? groupName, string? categoryName)
    {
        return new AssetCustomFieldResponse(
            field.Id,
            field.Scope,
            field.FieldName,
            field.DisplayLabel,
            field.FieldType,
            field.GroupId,
            groupName,
            field.DisplayOrder,
            field.AppliesToCategoryId,
            categoryName,
            field.ComponentType,
            field.Required,
            field.ShowInDetails,
            field.IncludeInSearch,
            field.Status,
            field.DefaultValue,
            field.Description,
            field.HelpText,
            field.Options,
            field.CompoundParts.Select(part => new CustomFieldPartDto(part.Key, part.Label, part.Unit)).ToList(),
            field.LookupSource,
            field.CreatedBy,
            field.CreatedAt,
            field.UpdatedBy,
            field.UpdatedAt,
            field.ClientId,
            field.TenantId);
    }
}

public sealed record AssetCustomFieldListResponse(
    IReadOnlyCollection<AssetCustomFieldResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// A field as written to and read from an export file. Groups and categories are named rather than referenced
/// by id, so a file exported from one environment can be imported into another.
/// </summary>
public sealed record AssetCustomFieldExportItem(
    string? Scope,
    string? FieldName,
    string? DisplayLabel,
    string? FieldType,
    string? GroupName,
    string? AppliesToCategoryCode,
    string? ComponentType,
    bool? Required,
    bool? ShowInDetails,
    bool? IncludeInSearch,
    string? Status,
    string? DefaultValue,
    string? Description,
    string? HelpText,
    List<string>? Options,
    List<CustomFieldPartDto>? CompoundParts,
    string? LookupSource);

public sealed record ImportItemError(string? Name, string Message);

public sealed record ImportResult(int Created, int Updated, IReadOnlyList<ImportItemError> Errors);

public sealed record SaveAssetFieldGroupRequest(
    string? GroupName,
    string? Description,
    string? Status,
    // The group's fields in display order. Leave out (null) to keep the current assignment unchanged.
    List<string>? FieldIds,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetFieldGroupFieldResponse(
    string Id,
    string FieldName,
    string DisplayLabel,
    string FieldType,
    string Scope,
    int DisplayOrder);

public sealed record AssetFieldGroupResponse(
    string Id,
    string GroupName,
    string? Description,
    string? Status,
    int FieldCount,
    IReadOnlyList<AssetFieldGroupFieldResponse> Fields,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static AssetFieldGroupResponse FromEntity(AssetFieldGroup group, IEnumerable<AssetCustomFieldDefinition> groupFields)
    {
        var fields = groupFields
            .OrderBy(field => field.DisplayOrder)
            .ThenBy(field => field.FieldName, StringComparer.Ordinal)
            .Select(field => new AssetFieldGroupFieldResponse(field.Id, field.FieldName, field.DisplayLabel, field.FieldType, field.Scope, field.DisplayOrder))
            .ToList();

        return new AssetFieldGroupResponse(
            group.Id,
            group.GroupName,
            group.Description,
            group.Status,
            fields.Count,
            fields,
            group.CreatedBy,
            group.CreatedAt,
            group.UpdatedBy,
            group.UpdatedAt);
    }
}

public sealed record AssetFieldGroupListResponse(
    IReadOnlyCollection<AssetFieldGroupResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>A group as written to and read from an export file; its fields are listed by name, in order.</summary>
public sealed record AssetFieldGroupExportItem(
    string? GroupName,
    string? Description,
    string? Status,
    List<string>? FieldNames);

public sealed record AssetFieldLayoutSectionDto(string? GroupId, List<string>? FieldNames);

public sealed record SaveAssetFieldLayoutRequest(
    string? LayoutName,
    // Leave empty for a layout that applies to all assets.
    string? AssetTypeId,
    string? Description,
    string? Status,
    List<AssetFieldLayoutSectionDto>? Sections,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

/// <param name="Source">"BuiltIn" for standard asset fields, otherwise the custom field's scope.</param>
public sealed record AvailableLayoutFieldResponse(
    string FieldName,
    string DisplayLabel,
    string FieldType,
    bool Required,
    string Source)
{
    public const string BuiltIn = "BuiltIn";
}

public sealed record AssetFieldLayoutSectionResponse(
    string GroupId,
    string? GroupName,
    IReadOnlyList<AvailableLayoutFieldResponse> Fields);

public sealed record AssetFieldLayoutResponse(
    string Id,
    string LayoutName,
    string? AssetTypeId,
    string AssetTypeName,
    string? Description,
    string? Status,
    int GroupCount,
    int FieldCount,
    IReadOnlyList<AssetFieldLayoutSectionResponse> Sections,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public const string AllAssets = "All Assets";
}

public sealed record AssetFieldLayoutListResponse(
    IReadOnlyCollection<AssetFieldLayoutResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);
