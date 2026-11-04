using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateMapLayerRequest(
    string? FloorPlanId,
    string? FloorPlanName,
    string? LocationPath,
    string? LayerName,
    string? LayerType,
    string? Description,
    bool? IsVisible,
    MapLayerDisplayOptionsRequest? DisplayOptions,
    MapLayerFiltersRequest? Filters,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record MapLayerDisplayOptionsRequest(
    bool? ShowAssetLabel,
    bool? ShowAssetId,
    bool? ShowAssetStatusIcon,
    bool? UseCategoryColor,
    string? LabelPosition);

/// <summary>AssetStatus "All Statuses" (or empty) means no status filter.</summary>
public sealed record MapLayerFiltersRequest(
    IReadOnlyList<string>? AssetCategories,
    string? AssetStatus);

public sealed record MapLayerResponse(
    string LayerId,
    string? FloorPlanId,
    string? FloorPlanName,
    string? LocationPath,
    string LayerName,
    string LayerType,
    string? Description,
    bool IsVisible,
    MapLayerDisplayOptionsResponse DisplayOptions,
    MapLayerFiltersResponse Filters,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static MapLayerResponse FromEntity(MapLayer layer)
    {
        var display = layer.DisplayOptions;

        return new MapLayerResponse(
            layer.LayerId,
            layer.FloorPlanId,
            layer.FloorPlanName,
            layer.LocationPath,
            layer.LayerName,
            layer.LayerType,
            layer.Description,
            layer.IsVisible,
            new MapLayerDisplayOptionsResponse(
                display.ShowAssetLabel,
                display.ShowAssetId,
                display.ShowAssetStatusIcon,
                display.UseCategoryColor,
                display.LabelPosition),
            new MapLayerFiltersResponse(
                layer.Filters.AssetCategories,
                layer.Filters.AssetStatus ?? MapLayerAssetStatus.AllStatuses),
            layer.Status,
            layer.CreatedBy,
            layer.CreatedAt,
            layer.UpdatedBy,
            layer.UpdatedAt,
            layer.TenantId,
            layer.ClientId);
    }
}

public sealed record MapLayerDisplayOptionsResponse(
    bool ShowAssetLabel,
    bool ShowAssetId,
    bool ShowAssetStatusIcon,
    bool UseCategoryColor,
    string LabelPosition);

public sealed record MapLayerFiltersResponse(
    IReadOnlyList<string> AssetCategories,
    string AssetStatus);

/// <summary>Choices for the Layer Type, Label Position and Asset Status dropdowns.</summary>
public sealed record MapLayerOptionsResponse(
    IReadOnlyList<string> LayerTypes,
    IReadOnlyList<string> LabelPositions,
    IReadOnlyList<string> AssetStatuses,
    IReadOnlyList<string> Statuses)
{
    public static readonly MapLayerOptionsResponse Instance = new(
        MapLayerType.All,
        MapLayerLabelPosition.All,
        [MapLayerAssetStatus.AllStatuses, .. MapLayerAssetStatus.All],
        MapLayerStatus.All);
}
