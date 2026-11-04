using A360.Administration.Api.Contracts;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Validation;

public static class MapLayerValidationExtensions
{
    private const int NameMaxLength = 150;
    private const int DescriptionMaxLength = 1000;
    private const int MaxAssetCategories = 50;

    public static Dictionary<string, string[]> Validate(this CreateMapLayerRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.LayerName), request.LayerName, NameMaxLength);
        Require(errors, nameof(request.LayerType), request.LayerType, NameMaxLength);

        OneOf(errors, nameof(request.LayerType), request.LayerType, MapLayerType.All);
        OneOf(errors, nameof(request.Status), request.Status, MapLayerStatus.All);
        OneOf(errors, "DisplayOptions.LabelPosition", request.DisplayOptions?.LabelPosition, MapLayerLabelPosition.All);

        var assetStatus = request.Filters?.AssetStatus;
        if (!string.IsNullOrWhiteSpace(assetStatus)
            && !string.Equals(assetStatus.Trim(), MapLayerAssetStatus.AllStatuses, StringComparison.OrdinalIgnoreCase))
        {
            OneOf(errors, "Filters.AssetStatus", assetStatus, MapLayerAssetStatus.All);
        }

        if (request.Filters?.AssetCategories is { Count: > MaxAssetCategories })
        {
            errors["Filters.AssetCategories"] = [$"At most {MaxAssetCategories} asset categories can be selected"];
        }

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors[nameof(request.Description)] = [$"Description must be at most {DescriptionMaxLength} characters"];
        }

        return errors;
    }

    /// <summary>The allowed value matching ignoring case, or null when missing or not allowed.</summary>
    public static string? Normalize(string? value, IReadOnlyList<string> allowed)
    {
        return allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} must be at most {maxLength} characters"];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && Normalize(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }
}
