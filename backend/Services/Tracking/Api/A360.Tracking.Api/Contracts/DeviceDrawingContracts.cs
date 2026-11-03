using System.Text.Json.Serialization;
using DeviceDrawingEntity = A360.Tracking.Domain.Entities.DeviceDrawing;

namespace A360.Tracking.Api.Contracts;

public sealed record CreateDeviceDrawingRequest(
    string? AreaId,
    bool AssemblyPoint,
    string? BuildingId,
    string? ClientId,
    string? CountryId,
    DateTime? CreatedAt,
    string? CreatedBy,
    GeoJsonFeatureCollectionContract? DeviceGeoJsonData,
    string? DeviceName,
    string? DeviceReferenceId,
    string? DeviceUniqueId,
    string? Exit,
    string? FloorId,
    string? Id,
    string? Priority,
    string? ProjectId,
    bool Status,
    string? TopZone,
    string? UserId,
    string? ZoneId,
    string? ZoneName);

public sealed record DeviceDrawingResponse(
    string Id,
    string AreaId,
    bool AssemblyPoint,
    string BuildingId,
    string? ClientId,
    string CountryId,
    DateTime? CreatedAt,
    string? CreatedBy,
    GeoJsonFeatureCollectionContract DeviceGeoJsonData,
    string DeviceName,
    string DeviceReferenceId,
    string DeviceUniqueId,
    string? Exit,
    string FloorId,
    string? Priority,
    string ProjectId,
    bool Status,
    string? TopZone,
    string? UserId,
    string ZoneId,
    string ZoneName)
{
    public static DeviceDrawingResponse FromEntity(DeviceDrawingEntity deviceDrawing)
    {
        return new DeviceDrawingResponse(
            deviceDrawing.Id,
            deviceDrawing.AreaId,
            deviceDrawing.AssemblyPoint,
            deviceDrawing.BuildingId,
            deviceDrawing.ClientId,
            deviceDrawing.CountryId,
            deviceDrawing.CreatedAt,
            deviceDrawing.CreatedBy,
            GeoJsonConversion.ToContract(deviceDrawing.DeviceGeoJsonData),
            deviceDrawing.DeviceName,
            deviceDrawing.DeviceReferenceId,
            deviceDrawing.DeviceUniqueId,
            deviceDrawing.Exit,
            deviceDrawing.FloorId,
            deviceDrawing.Priority,
            deviceDrawing.ProjectId,
            deviceDrawing.Status,
            deviceDrawing.TopZone,
            deviceDrawing.UserId,
            deviceDrawing.ZoneId,
            deviceDrawing.ZoneName);
    }
}

// Property names (including the "cutomer" spelling) match what the front end already consumes.
public sealed record CustomerAnalyticsDrawingResponse(
    [property: JsonPropertyName("cutomerAnalyticsDrawZones")] IReadOnlyList<ZoneDrawingResponse> DrawZones,
    [property: JsonPropertyName("cutomerAnalyticsDrawdevices")] IReadOnlyList<DeviceDrawingResponse> DrawDevices);
