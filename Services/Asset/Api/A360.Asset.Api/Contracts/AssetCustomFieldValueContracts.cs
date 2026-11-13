using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>One field on the Custom Field Values page, with the value recorded for the asset.</summary>
/// <param name="FieldId">Definition id; null for a value whose field is no longer defined.</param>
/// <param name="FieldName">Stable field key (e.g. "warranty_expiry"); the label for undefined values.</param>
/// <param name="Value">As recorded (dates as yyyy-MM-dd); null when not set.</param>
/// <param name="IsMissing">Required and not set: shown as "Missing".</param>
/// <param name="Source">"Asset" (defined for every asset), "Category" (for the asset's category), "Category Field"
/// (added on the category itself), or "Unlisted" (a recorded value whose field is not defined any more).</param>
public sealed record AssetCustomFieldValueResponse(
    string? FieldId,
    string FieldName,
    string Label,
    string? FieldType,
    string? Value,
    bool IsFilled,
    bool Required,
    bool IsMissing,
    string Source,
    string? CategoryName,
    string? Description,
    string? HelpText,
    IReadOnlyList<string> Options,
    IReadOnlyList<AssetCustomFieldValuePart> CompoundParts,
    bool IsActive);

public sealed record AssetCustomFieldValuePart(string Key, string Label, string? Unit);

/// <summary>A card on the page, e.g. "Purchase &amp; Warranty".</summary>
/// <param name="GroupId">Field group id; null for "Other Fields", category fields and unlisted values.</param>
public sealed record AssetCustomFieldValueGroupResponse(
    string? GroupId,
    string GroupName,
    int Total,
    int Filled,
    int RequiredMissing,
    IReadOnlyList<AssetCustomFieldValueResponse> Fields);

/// <param name="AppliesTo">Where the fields come from, for "defined for all assets, plus the Power Generation category".</param>
public sealed record AssetCustomFieldValuesResponse(
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    IReadOnlyList<string> AppliesTo,
    int Total,
    int Filled,
    int Empty,
    int RequiredMissing,
    IReadOnlyList<AssetCustomFieldValueGroupResponse> Groups);

/// <summary>
/// Matches the custom field values recorded on an asset (additional_information.custom_fields) with the fields that
/// apply to it: every Asset-scope field, the Category-scope fields of its category and parent categories, and the
/// fields added on those categories. Values are matched to fields by field name or label, ignoring case, spaces and
/// underscores. Values whose field is not defined are listed under "Additional Fields" so nothing recorded is hidden.
/// </summary>
public static class AssetCustomFieldValues
{
    public const string OtherFieldsGroup = "Other Fields";
    public const string UnlistedGroup = "Additional Fields";

    public static AssetCustomFieldValuesResponse Build(
        AssetRegistration asset,
        IReadOnlyCollection<AssetCustomFieldDefinition> definitions,
        IReadOnlyCollection<AssetFieldGroup> fieldGroups,
        IReadOnlyCollection<AssetCategory> categories,
        bool emptyOnly)
    {
        var assetCategories = CategoriesOf(asset, categories);
        var categoryIds = assetCategories.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        var categoryNames = assetCategories.ToDictionary(category => category.Id, category => category.Name, StringComparer.Ordinal);

        var values = new Dictionary<string, AssetCustomField>(StringComparer.Ordinal);
        foreach (var value in asset.AdditionalInformation?.CustomFields ?? [])
        {
            values.TryAdd(Key(value.FieldName), value);
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        AssetCustomField? TakeValue(params string?[] names)
        {
            foreach (var name in names)
            {
                var key = Key(name);
                if (key.Length > 0 && values.TryGetValue(key, out var value))
                {
                    used.Add(key);
                    return value;
                }
            }

            return null;
        }

        var applicable = definitions
            .Where(definition => definition.Scope == CustomFieldScopes.Asset
                || (definition.Scope == CustomFieldScopes.Category && definition.AppliesToCategoryId is not null && categoryIds.Contains(definition.AppliesToCategoryId)))
            .ToList();

        var groupsById = fieldGroups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var cards = new List<(string? GroupId, string Name, int Order, List<AssetCustomFieldValueResponse> Fields)>();

        // Field groups first, in the order they were created; fields in their display order.
        var orderedGroups = fieldGroups.OrderBy(group => group.CreatedAt).ThenBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var group in orderedGroups)
        {
            var fields = applicable
                .Where(definition => definition.GroupId == group.Id)
                .OrderBy(definition => definition.DisplayOrder)
                .ThenBy(definition => definition.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .Select(definition => FromDefinition(definition, TakeValue(definition.FieldName, definition.DisplayLabel), categoryNames))
                .Where(field => field is not null)
                .Select(field => field!)
                .ToList();
            cards.Add((group.Id, group.GroupName, cards.Count, fields));
        }

        // Fields without a group (or whose group was deleted).
        var ungrouped = applicable
            .Where(definition => definition.GroupId is null || !groupsById.ContainsKey(definition.GroupId))
            .OrderBy(definition => definition.CreatedAt)
            .ThenBy(definition => definition.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .Select(definition => FromDefinition(definition, TakeValue(definition.FieldName, definition.DisplayLabel), categoryNames))
            .Where(field => field is not null)
            .Select(field => field!)
            .ToList();
        cards.Add((null, OtherFieldsGroup, cards.Count, ungrouped));

        // Fields added on the asset's categories (Asset Categories › Custom Fields), one card per category.
        foreach (var category in assetCategories)
        {
            var fields = category.CustomFields
                .Where(field => !string.IsNullOrWhiteSpace(field.FieldName) && !used.Contains(Key(field.FieldName)))
                .Select(field =>
                {
                    var value = TakeValue(field.FieldName);
                    var filled = !string.IsNullOrWhiteSpace(value?.Value);
                    return new AssetCustomFieldValueResponse(
                        field.Id, field.FieldName, field.FieldName, string.IsNullOrWhiteSpace(field.Type) ? value?.FieldType : field.Type, filled ? value!.Value : null,
                        filled, field.Required, field.Required && !filled, "Category Field", category.Name,
                        null, null, [], [], true);
                })
                .ToList();
            cards.Add((null, $"{category.Name} Fields", cards.Count, fields));
        }

        // Recorded values that no current field matches.
        var unlisted = (asset.AdditionalInformation?.CustomFields ?? [])
            .Where(value => !used.Contains(Key(value.FieldName)) && !string.IsNullOrWhiteSpace(value.FieldName))
            .GroupBy(value => Key(value.FieldName))
            .Select(group => group.First())
            .Select(value =>
            {
                var filled = !string.IsNullOrWhiteSpace(value.Value);
                return new AssetCustomFieldValueResponse(
                    null, value.FieldName, value.FieldName, value.FieldType, filled ? value.Value : null,
                    filled, false, false, "Unlisted", null, null, null, [], [], true);
            })
            .ToList();
        cards.Add((null, UnlistedGroup, cards.Count, unlisted));

        var allFields = cards.SelectMany(card => card.Fields).ToList();
        var groups = cards
            .Select(card =>
            {
                var shown = emptyOnly ? card.Fields.Where(field => !field.IsFilled).ToList() : card.Fields;
                return new AssetCustomFieldValueGroupResponse(
                    card.GroupId,
                    card.Name,
                    card.Fields.Count,
                    card.Fields.Count(field => field.IsFilled),
                    card.Fields.Count(field => field.IsMissing),
                    shown);
            })
            .Where(group => group.Fields.Count > 0)
            .ToList();

        var appliesTo = new List<string> { "All assets" };
        appliesTo.AddRange(assetCategories.Select(category => category.Name));

        return new AssetCustomFieldValuesResponse(
            asset.AssetRegistrationId,
            asset.BasicInformation?.AssetName,
            asset.BasicInformation?.AssetTag,
            appliesTo,
            allFields.Count,
            allFields.Count(field => field.IsFilled),
            allFields.Count(field => !field.IsFilled),
            allFields.Count(field => field.IsMissing),
            groups);
    }

    /// <summary>Inactive fields are shown only when the asset has a value for them.</summary>
    private static AssetCustomFieldValueResponse? FromDefinition(
        AssetCustomFieldDefinition definition,
        AssetCustomField? value,
        IReadOnlyDictionary<string, string> categoryNames)
    {
        var isActive = !string.Equals(definition.Status, "Inactive", StringComparison.OrdinalIgnoreCase);
        var filled = !string.IsNullOrWhiteSpace(value?.Value);
        if (!isActive && !filled)
        {
            return null;
        }

        var required = definition.Required && isActive;
        return new AssetCustomFieldValueResponse(
            definition.Id,
            definition.FieldName,
            string.IsNullOrWhiteSpace(definition.DisplayLabel) ? definition.FieldName : definition.DisplayLabel,
            definition.FieldType,
            filled ? value!.Value : null,
            filled,
            required,
            required && !filled,
            definition.Scope,
            definition.AppliesToCategoryId is not null ? categoryNames.GetValueOrDefault(definition.AppliesToCategoryId) : null,
            definition.Description,
            definition.HelpText,
            definition.Options,
            definition.CompoundParts.Select(part => new AssetCustomFieldValuePart(part.Key, part.Label, part.Unit)).ToList(),
            isActive);
    }

    /// <summary>
    /// The asset's category and sub-category (matched by name, sub-category under its category when possible),
    /// plus every parent category above them.
    /// </summary>
    private static List<AssetCategory> CategoriesOf(AssetRegistration asset, IReadOnlyCollection<AssetCategory> categories)
    {
        var active = categories.Where(category => !category.IsDeleted).ToList();
        var byId = active.ToDictionary(category => category.Id, StringComparer.Ordinal);
        bool Named(AssetCategory category, string? name) =>
            !string.IsNullOrWhiteSpace(name) && string.Equals(category.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

        var classification = asset.Classification;
        var main = active.Where(category => Named(category, classification?.Category)).ToList();
        var mainIds = main.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        var subs = active.Where(category => Named(category, classification?.SubCategory)).ToList();
        var subsUnderMain = subs.Where(category => category.ParentCategoryId is not null && mainIds.Contains(category.ParentCategoryId)).ToList();

        var result = new List<AssetCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in main.Concat(subsUnderMain.Count > 0 ? subsUnderMain : subs))
        {
            // Walk up to the top, parents first.
            var chain = new List<AssetCategory>();
            for (var current = start; current is not null && chain.Count < 20; current = current.ParentCategoryId is null ? null : byId.GetValueOrDefault(current.ParentCategoryId))
            {
                chain.Insert(0, current);
            }

            foreach (var category in chain.Where(category => seen.Add(category.Id)))
            {
                result.Add(category);
            }
        }

        return result;
    }

    /// <summary>"Warranty Expiry", "warranty_expiry" and "warranty-expiry" all match.</summary>
    private static string Key(string? name) =>
        new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
