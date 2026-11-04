using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

/// <summary>
/// Save Changes on General Settings. Every section and field is optional; anything left out takes its default,
/// so an empty body is the same as Reset to Default.
/// </summary>
public sealed record SaveMapGeneralSettingsRequest(
    MapManagementSettingsDto? MapManagement,
    MapPlottingDisplaySettingsDto? PlottingDisplay,
    MapZoneGeofenceSettingsDto? ZoneGeofence,
    MapVersioningSettingsDto? VersioningPublishing,
    MapAccessSecuritySettingsDto? AccessSecurity,
    MapNotificationSettingsDto? Notifications,
    string? UpdatedBy,
    string? TenantId,
    string? ClientId)
{
    /// <summary>Replaces every section of the settings with the request, filling in defaults.</summary>
    public void ApplyTo(MapGeneralSettings settings)
    {
        var map = MapManagement;
        var mapDefaults = new MapManagementSettings();
        settings.MapManagement = new MapManagementSettings
        {
            DefaultMapView = Pick(map?.DefaultMapView, MapSettingsOptions.DefaultMapViews) ?? mapDefaults.DefaultMapView,
            DefaultZoomLevel = map?.DefaultZoomLevel ?? mapDefaults.DefaultZoomLevel,
            MeasurementUnit = Pick(map?.MeasurementUnit, MapSettingsOptions.MeasurementUnits) ?? mapDefaults.MeasurementUnit,
            SupportedFileTypes = PickMany(map?.SupportedFileTypes, MapSettingsOptions.FileTypes) ?? mapDefaults.SupportedFileTypes,
            MaxFileSizeMb = map?.MaxFileSizeMb ?? mapDefaults.MaxFileSizeMb,
            DefaultCoordinateSystem = Pick(map?.DefaultCoordinateSystem, MapSettingsOptions.CoordinateSystems) ?? mapDefaults.DefaultCoordinateSystem
        };

        var plot = PlottingDisplay;
        var plotDefaults = new MapPlottingDisplaySettings();
        settings.PlottingDisplay = new MapPlottingDisplaySettings
        {
            DefaultPlottedItems = PickMany(plot?.DefaultPlottedItems, MapSettingsOptions.PlottedItems) ?? plotDefaults.DefaultPlottedItems,
            AssetLabelDisplay = Pick(plot?.AssetLabelDisplay, MapSettingsOptions.AssetLabelDisplays) ?? plotDefaults.AssetLabelDisplay,
            LabelPosition = Pick(plot?.LabelPosition, MapLayerLabelPosition.All) ?? plotDefaults.LabelPosition,
            ShowAssetStatusIcon = plot?.ShowAssetStatusIcon ?? plotDefaults.ShowAssetStatusIcon,
            SnapToGrid = plot?.SnapToGrid ?? plotDefaults.SnapToGrid,
            GridSize = plot?.GridSize is { } gridSize ? Math.Round(gridSize, 2) : plotDefaults.GridSize
        };

        var zone = ZoneGeofence;
        var zoneDefaults = new MapZoneGeofenceSettings();
        settings.ZoneGeofence = new MapZoneGeofenceSettings
        {
            DefaultZoneType = Pick(zone?.DefaultZoneType, MapZoneType.All) ?? zoneDefaults.DefaultZoneType,
            DefaultGeofenceDwellTimeMinutes = zone?.DefaultGeofenceDwellTimeMinutes ?? zoneDefaults.DefaultGeofenceDwellTimeMinutes,
            ZoneColorScheme = zone?.ZoneColorScheme is { Count: > 0 } colors
                ? colors.Select(color => color.Trim().ToUpperInvariant()).Distinct().ToList()
                : zoneDefaults.ZoneColorScheme,
            AllowZoneOverlap = zone?.AllowZoneOverlap ?? zoneDefaults.AllowZoneOverlap,
            GeofenceEventLogging = zone?.GeofenceEventLogging ?? zoneDefaults.GeofenceEventLogging
        };

        var version = VersioningPublishing;
        var versionDefaults = new MapVersioningSettings();
        settings.VersioningPublishing = new MapVersioningSettings
        {
            VersionNumbering = Pick(version?.VersionNumbering, MapSettingsOptions.VersionNumberings) ?? versionDefaults.VersionNumbering,
            RequireApprovalToPublish = version?.RequireApprovalToPublish ?? versionDefaults.RequireApprovalToPublish,
            KeepVersionHistory = version?.KeepVersionHistory ?? versionDefaults.KeepVersionHistory,
            AutoArchiveOlderVersions = version?.AutoArchiveOlderVersions ?? versionDefaults.AutoArchiveOlderVersions,
            ArchiveAfterDays = version?.ArchiveAfterDays ?? versionDefaults.ArchiveAfterDays
        };

        var access = AccessSecurity;
        var accessDefaults = new MapAccessSecuritySettings();
        settings.AccessSecurity = new MapAccessSecuritySettings
        {
            DefaultMapAccess = Pick(access?.DefaultMapAccess, MapSettingsOptions.MapAccessLevels) ?? accessDefaults.DefaultMapAccess,
            AllowDownloadOfMapFiles = access?.AllowDownloadOfMapFiles ?? accessDefaults.AllowDownloadOfMapFiles,
            WatermarkPublishedMaps = access?.WatermarkPublishedMaps ?? accessDefaults.WatermarkPublishedMaps
        };

        var notify = Notifications;
        var notifyDefaults = new MapNotificationSettings();
        settings.Notifications = new MapNotificationSettings
        {
            NotifyOnMapPublication = notify?.NotifyOnMapPublication ?? notifyDefaults.NotifyOnMapPublication,
            NotifyOnGeofenceEvents = notify?.NotifyOnGeofenceEvents ?? notifyDefaults.NotifyOnGeofenceEvents,
            NotifyOnVersionChange = notify?.NotifyOnVersionChange ?? notifyDefaults.NotifyOnVersionChange
        };
    }

    private static string? Pick(string? value, IReadOnlyList<string> allowed) =>
        MapGeneralSettingsValidationExtensions.Normalize(value, allowed);

    // Null when not given, so the default list applies; an empty list is kept as "none selected".
    private static List<string>? PickMany(IReadOnlyList<string>? values, IReadOnlyList<string> allowed) =>
        values?.Select(value => Pick(value, allowed)!).Distinct().ToList();
}

public sealed record MapManagementSettingsDto(
    string? DefaultMapView,
    int? DefaultZoomLevel,
    string? MeasurementUnit,
    IReadOnlyList<string>? SupportedFileTypes,
    int? MaxFileSizeMb,
    string? DefaultCoordinateSystem);

public sealed record MapPlottingDisplaySettingsDto(
    IReadOnlyList<string>? DefaultPlottedItems,
    string? AssetLabelDisplay,
    string? LabelPosition,
    bool? ShowAssetStatusIcon,
    bool? SnapToGrid,
    double? GridSize);

public sealed record MapZoneGeofenceSettingsDto(
    string? DefaultZoneType,
    int? DefaultGeofenceDwellTimeMinutes,
    IReadOnlyList<string>? ZoneColorScheme,
    bool? AllowZoneOverlap,
    bool? GeofenceEventLogging);

public sealed record MapVersioningSettingsDto(
    string? VersionNumbering,
    bool? RequireApprovalToPublish,
    int? KeepVersionHistory,
    bool? AutoArchiveOlderVersions,
    int? ArchiveAfterDays);

public sealed record MapAccessSecuritySettingsDto(
    string? DefaultMapAccess,
    bool? AllowDownloadOfMapFiles,
    bool? WatermarkPublishedMaps);

public sealed record MapNotificationSettingsDto(
    bool? NotifyOnMapPublication,
    bool? NotifyOnGeofenceEvents,
    bool? NotifyOnVersionChange);

public sealed record MapGeneralSettingsResponse(
    string? SettingsId,
    bool IsDefault,
    MapManagementSettings MapManagement,
    MapPlottingDisplaySettings PlottingDisplay,
    MapZoneGeofenceSettings ZoneGeofence,
    MapVersioningSettings VersioningPublishing,
    MapAccessSecuritySettings AccessSecurity,
    MapNotificationSettings Notifications,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static MapGeneralSettingsResponse FromEntity(MapGeneralSettings settings, bool isDefault = false)
    {
        return new MapGeneralSettingsResponse(
            isDefault ? null : settings.SettingsId,
            isDefault,
            settings.MapManagement,
            settings.PlottingDisplay,
            settings.ZoneGeofence,
            settings.VersioningPublishing,
            settings.AccessSecurity,
            settings.Notifications,
            settings.CreatedBy,
            settings.CreatedAt,
            settings.UpdatedBy,
            settings.UpdatedAt,
            settings.TenantId,
            settings.ClientId);
    }
}

/// <summary>Choices for every General Settings dropdown and checkbox group.</summary>
public sealed record MapGeneralSettingsOptionsResponse(
    IReadOnlyList<string> DefaultMapViews,
    IReadOnlyList<string> MeasurementUnits,
    IReadOnlyList<string> FileTypes,
    IReadOnlyList<string> CoordinateSystems,
    IReadOnlyList<string> PlottedItems,
    IReadOnlyList<string> AssetLabelDisplays,
    IReadOnlyList<string> LabelPositions,
    IReadOnlyList<string> ZoneTypes,
    IReadOnlyList<string> VersionNumberings,
    IReadOnlyList<string> MapAccessLevels)
{
    public static readonly MapGeneralSettingsOptionsResponse Instance = new(
        MapSettingsOptions.DefaultMapViews,
        MapSettingsOptions.MeasurementUnits,
        MapSettingsOptions.FileTypes,
        MapSettingsOptions.CoordinateSystems,
        MapSettingsOptions.PlottedItems,
        MapSettingsOptions.AssetLabelDisplays,
        MapLayerLabelPosition.All,
        MapZoneType.All,
        MapSettingsOptions.VersionNumberings,
        MapSettingsOptions.MapAccessLevels);
}
