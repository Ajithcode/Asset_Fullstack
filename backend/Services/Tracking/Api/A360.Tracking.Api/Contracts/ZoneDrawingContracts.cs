using System.Text.Json;
using ZoneDrawingEntity = A360.Tracking.Domain.Entities.ZoneDrawing;

namespace A360.Tracking.Api.Contracts;

public sealed record CreateZoneDrawingRequest(
    string? AreaId,
    bool AssemblyPoint,
    string? BuildingId,
    string? ClientId,
    string? CountryId,
    DateTime? CreatedAt,
    string? CreatedBy,
    string? Exit,
    string? FloorId,
    GeoJsonFeatureCollectionContract? GeoJsonData,
    string? Id,
    string? Priority,
    string? ProjectId,
    bool Status,
    string? TopZone,
    string? ZoneId,
    string? ZoneName);

public sealed record GeoJsonFeatureCollectionContract(
    string? Type,
    List<GeoJsonFeatureContract>? Features);

public sealed record GeoJsonFeatureContract(
    string? Type,
    GeoJsonGeometryContract? Geometry,
    JsonElement? Properties);

public sealed record GeoJsonGeometryContract(
    string? Type,
    JsonElement Coordinates);

public sealed record ZoneDrawingResponse(
    string Id,
    string AreaId,
    bool AssemblyPoint,
    string BuildingId,
    string? ClientId,
    string CountryId,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? Exit,
    string FloorId,
    GeoJsonFeatureCollectionContract GeoJsonData,
    string? Priority,
    string ProjectId,
    bool Status,
    string? TopZone,
    string ZoneId,
    string ZoneName)
{
    public static ZoneDrawingResponse FromEntity(ZoneDrawingEntity zoneDrawing)
    {
        return new ZoneDrawingResponse(
            zoneDrawing.Id,
            zoneDrawing.AreaId,
            zoneDrawing.AssemblyPoint,
            zoneDrawing.BuildingId,
            zoneDrawing.ClientId,
            zoneDrawing.CountryId,
            zoneDrawing.CreatedBy,
            zoneDrawing.CreatedAt,
            zoneDrawing.Exit,
            zoneDrawing.FloorId,
            GeoJsonConversion.ToContract(zoneDrawing.GeoJsonData),
            zoneDrawing.Priority,
            zoneDrawing.ProjectId,
            zoneDrawing.Status,
            zoneDrawing.TopZone,
            zoneDrawing.ZoneId,
            zoneDrawing.ZoneName);
    }
}
