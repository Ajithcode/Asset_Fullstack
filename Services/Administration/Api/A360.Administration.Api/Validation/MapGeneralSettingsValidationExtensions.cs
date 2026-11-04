using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Validation;

public static partial class MapGeneralSettingsValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this SaveMapGeneralSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var map = request.MapManagement;
        OneOf(errors, "MapManagement.DefaultMapView", map?.DefaultMapView, MapSettingsOptions.DefaultMapViews);
        Range(errors, "MapManagement.DefaultZoomLevel", map?.DefaultZoomLevel, 10, 400);
        OneOf(errors, "MapManagement.MeasurementUnit", map?.MeasurementUnit, MapSettingsOptions.MeasurementUnits);
        AllOf(errors, "MapManagement.SupportedFileTypes", map?.SupportedFileTypes, MapSettingsOptions.FileTypes);
        if (map?.SupportedFileTypes is { Count: 0 })
        {
            errors["MapManagement.SupportedFileTypes"] = ["Select at least one supported file type"];
        }

        Range(errors, "MapManagement.MaxFileSizeMb", map?.MaxFileSizeMb, 1, 1024);
        OneOf(errors, "MapManagement.DefaultCoordinateSystem", map?.DefaultCoordinateSystem, MapSettingsOptions.CoordinateSystems);

        var plot = request.PlottingDisplay;
        AllOf(errors, "PlottingDisplay.DefaultPlottedItems", plot?.DefaultPlottedItems, MapSettingsOptions.PlottedItems);
        OneOf(errors, "PlottingDisplay.AssetLabelDisplay", plot?.AssetLabelDisplay, MapSettingsOptions.AssetLabelDisplays);
        OneOf(errors, "PlottingDisplay.LabelPosition", plot?.LabelPosition, MapLayerLabelPosition.All);
        if (plot?.GridSize is { } gridSize && (gridSize <= 0 || gridSize > 100 || !double.IsFinite(gridSize)))
        {
            errors["PlottingDisplay.GridSize"] = ["PlottingDisplay.GridSize must be greater than 0 and at most 100 m"];
        }

        var zone = request.ZoneGeofence;
        OneOf(errors, "ZoneGeofence.DefaultZoneType", zone?.DefaultZoneType, MapZoneType.All);
        Range(errors, "ZoneGeofence.DefaultGeofenceDwellTimeMinutes", zone?.DefaultGeofenceDwellTimeMinutes, 1, 1440);
        if (zone?.ZoneColorScheme is { } colors)
        {
            if (colors.Count > 20)
            {
                errors["ZoneGeofence.ZoneColorScheme"] = ["At most 20 zone colours can be set"];
            }
            else if (colors.Any(color => string.IsNullOrWhiteSpace(color) || !HexColorPattern().IsMatch(color.Trim())))
            {
                errors["ZoneGeofence.ZoneColorScheme"] = ["Zone colours must be hex colours such as #7C3AED"];
            }
        }

        var version = request.VersioningPublishing;
        OneOf(errors, "VersioningPublishing.VersionNumbering", version?.VersionNumbering, MapSettingsOptions.VersionNumberings);
        Range(errors, "VersioningPublishing.KeepVersionHistory", version?.KeepVersionHistory, 1, 100);
        Range(errors, "VersioningPublishing.ArchiveAfterDays", version?.ArchiveAfterDays, 1, 3650);

        OneOf(errors, "AccessSecurity.DefaultMapAccess", request.AccessSecurity?.DefaultMapAccess, MapSettingsOptions.MapAccessLevels);

        return errors;
    }

    /// <summary>The allowed value matching ignoring case, or null when missing or not allowed.</summary>
    public static string? Normalize(string? value, IReadOnlyList<string> allowed)
    {
        return allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && Normalize(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void AllOf(Dictionary<string, string[]> errors, string field, IReadOnlyList<string>? values, IReadOnlyList<string> allowed)
    {
        if (values is not null && values.Any(value => Normalize(value, allowed) is null))
        {
            errors[field] = [$"{field} can only contain: {string.Join(", ", allowed)}"];
        }
    }

    private static void Range(Dictionary<string, string[]> errors, string field, int? value, int min, int max)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}"];
        }
    }

    [GeneratedRegex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$")]
    private static partial Regex HexColorPattern();
}
