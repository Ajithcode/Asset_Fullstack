using System.Globalization;
using System.Text.RegularExpressions;
using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static partial class AssetCustomFieldValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 500;
    private const int GroupDescriptionMaxLength = 250;
    private const int HelpTextMaxLength = 250;

    // Lowercase letters, digits and underscores, starting with a letter, e.g. "warranty_expiry".
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex FieldNamePattern();

    public static bool IsValidFieldName(string? fieldName) => fieldName is not null && FieldNamePattern().IsMatch(fieldName.Trim());

    public static Dictionary<string, string[]> Validate(this SaveAssetCustomFieldRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var scope = CustomFieldScopes.Normalize(request.Scope);
        if (scope is null)
        {
            errors["Scope"] = [$"Scope must be one of: {string.Join(", ", CustomFieldScopes.All)}"];
        }

        if (string.IsNullOrWhiteSpace(request.FieldName))
        {
            errors["FieldName"] = ["FieldName is required"];
        }
        else if (!IsValidFieldName(request.FieldName) || request.FieldName.Trim().Length > NameMaxLength)
        {
            errors["FieldName"] = ["FieldName must use lowercase letters, digits and underscores, start with a letter, and be at most 100 characters"];
        }
        else if (BuiltInAssetFields.Contains(request.FieldName.Trim()))
        {
            errors["FieldName"] = [$"'{request.FieldName.Trim()}' is a standard asset field and cannot be used for a custom field"];
        }

        RequireText(errors, "DisplayLabel", request.DisplayLabel, NameMaxLength);

        var fieldType = CustomFieldTypes.Normalize(request.FieldType);
        if (fieldType is null)
        {
            errors["FieldType"] = [$"FieldType must be one of: {string.Join(", ", CustomFieldTypes.All)}"];
        }

        if (scope == CustomFieldScopes.Category && string.IsNullOrWhiteSpace(request.AppliesToCategoryId))
        {
            errors["AppliesToCategoryId"] = ["AppliesToCategoryId is required for a category specific field"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !ActiveInactiveStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }

        MaxLength(errors, "Description", request.Description, DescriptionMaxLength);
        MaxLength(errors, "HelpText", request.HelpText, HelpTextMaxLength);

        if (fieldType == CustomFieldTypes.Compound
            && (request.CompoundParts ?? []).Where(part => !string.IsNullOrWhiteSpace(part?.Key)).GroupBy(part => part.Key!.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            errors["CompoundParts"] = ["CompoundParts keys must be unique"];
        }

        if (fieldType is not null && ContractText.TrimOrNull(request.DefaultValue) is { } defaultValue
            && DefaultValueProblem(fieldType, defaultValue, request.Options) is { } problem)
        {
            errors["DefaultValue"] = [problem];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SaveAssetFieldGroupRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "GroupName", request.GroupName, NameMaxLength);
        MaxLength(errors, "Description", request.Description, GroupDescriptionMaxLength);

        if (!string.IsNullOrWhiteSpace(request.Status) && !ActiveInactiveStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }

        if (request.FieldIds is { } fieldIds && fieldIds.Count != fieldIds.Distinct().Count())
        {
            errors["FieldIds"] = ["A field can only be listed once"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SaveAssetFieldLayoutRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "LayoutName", request.LayoutName, NameMaxLength);
        MaxLength(errors, "Description", request.Description, DescriptionMaxLength);

        if (!string.IsNullOrWhiteSpace(request.Status) && !ActiveInactiveStatuses.IsValid(request.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", ActiveInactiveStatuses.All)}"];
        }

        var sections = request.Sections ?? [];
        if (sections.Any(section => string.IsNullOrWhiteSpace(section?.GroupId)))
        {
            errors["Sections"] = ["Each section needs a GroupId"];
        }
        else if (sections.GroupBy(section => section.GroupId!.Trim()).Any(group => group.Count() > 1))
        {
            errors["Sections"] = ["A field group can only appear once in a layout"];
        }

        var fieldNames = sections.SelectMany(section => section?.FieldNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();
        if (fieldNames.Count != fieldNames.Distinct().Count())
        {
            errors["Sections.FieldNames"] = ["A field can only be placed once in a layout"];
        }

        return errors;
    }

    /// <summary>Checks a default value fits its field type; returns the problem, or null when it fits.</summary>
    private static string? DefaultValueProblem(string fieldType, string defaultValue, IEnumerable<string>? options)
    {
        return fieldType switch
        {
            CustomFieldTypes.Number when !decimal.TryParse(defaultValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                => "DefaultValue must be a number for a Number field",
            CustomFieldTypes.Date when !DateTime.TryParse(defaultValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                => "DefaultValue must be a date (e.g. 2026-01-31) for a Date field",
            CustomFieldTypes.Checkbox when !bool.TryParse(defaultValue, out _)
                => "DefaultValue must be true or false for a Checkbox field",
            CustomFieldTypes.Dropdown when options?.Any() == true
                                           && !options.Any(option => string.Equals(option?.Trim(), defaultValue, StringComparison.OrdinalIgnoreCase))
                => "DefaultValue must be one of the Options for a Dropdown field",
            _ => null
        };
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else
        {
            MaxLength(errors, field, value, maxLength);
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
