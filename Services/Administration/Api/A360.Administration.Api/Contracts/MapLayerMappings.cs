using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class MapLayerMappings
{
    public static MapLayer ToEntity(this CreateMapLayerRequest request, CurrentUser? currentUser)
    {
        var display = request.DisplayOptions;
        var filters = request.Filters;

        return new MapLayer
        {
            // Generated on the server so every layer gets a stable id independent of the Mongo _id.
            LayerId = Guid.NewGuid().ToString(),
            FloorPlanId = Optional(request.FloorPlanId),
            FloorPlanName = Optional(request.FloorPlanName),
            LocationPath = Optional(request.LocationPath),
            LayerName = request.LayerName!.Trim(),
            LayerType = MapLayerValidationExtensions.Normalize(request.LayerType, MapLayerType.All)!,
            Description = Optional(request.Description),
            IsVisible = request.IsVisible ?? true,
            DisplayOptions = new MapLayerDisplayOptions
            {
                ShowAssetLabel = display?.ShowAssetLabel ?? false,
                ShowAssetId = display?.ShowAssetId ?? false,
                ShowAssetStatusIcon = display?.ShowAssetStatusIcon ?? false,
                UseCategoryColor = display?.UseCategoryColor ?? false,
                LabelPosition = MapLayerValidationExtensions.Normalize(display?.LabelPosition, MapLayerLabelPosition.All)
                    ?? MapLayerLabelPosition.Default
            },
            Filters = new MapLayerFilters
            {
                AssetCategories = (filters?.AssetCategories ?? [])
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Select(category => category.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                // "All Statuses" is stored as no filter.
                AssetStatus = MapLayerValidationExtensions.Normalize(filters?.AssetStatus, MapLayerAssetStatus.All)
            },
            Status = MapLayerValidationExtensions.Normalize(request.Status, MapLayerStatus.All) ?? MapLayerStatus.Active,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
