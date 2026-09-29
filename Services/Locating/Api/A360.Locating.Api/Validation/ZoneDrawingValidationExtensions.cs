using System.Text.Json;
using A360.Locating.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.Locating.Api.Validation;

public static class ZoneDrawingValidationExtensions
{
    private static readonly HashSet<string> GeometryTypes = new(StringComparer.Ordinal)
    {
        "Point", "MultiPoint", "LineString", "MultiLineString", "Polygon", "MultiPolygon"
    };

    public static Dictionary<string, string[]> Validate(this CreateZoneDrawingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireObjectId(errors, "ProjectId", request.ProjectId);
        RequireObjectId(errors, "CountryId", request.CountryId);
        RequireObjectId(errors, "AreaId", request.AreaId);
        RequireObjectId(errors, "BuildingId", request.BuildingId);
        RequireObjectId(errors, "FloorId", request.FloorId);
        RequireObjectId(errors, "ZoneId", request.ZoneId);

        if (string.IsNullOrWhiteSpace(request.ZoneName))
        {
            errors["ZoneName"] = ["ZoneName is required"];
        }

        ValidateGeoJson(errors, "GeoJsonData", request.GeoJsonData);

        return errors;
    }

    internal static void RequireObjectId(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (!MongoObjectId.IsValid(value.Trim()))
        {
            errors[field] = [$"{field} is not a valid id"];
        }
    }

    internal static void ValidateGeoJson(Dictionary<string, string[]> errors, string field, GeoJsonFeatureCollectionContract? geoJson)
    {
        if (geoJson is null)
        {
            errors[field] = [$"{field} is required"];
            return;
        }

        if (geoJson.Type != "FeatureCollection")
        {
            errors[$"{field}.Type"] = [$"{field} type must be 'FeatureCollection'"];
        }

        if (geoJson.Features is null || geoJson.Features.Count == 0)
        {
            errors[$"{field}.Features"] = [$"{field} must contain at least one feature"];
            return;
        }

        for (var index = 0; index < geoJson.Features.Count; index++)
        {
            var feature = geoJson.Features[index];
            var path = $"{field}.Features[{index}]";

            if (feature.Type != "Feature")
            {
                errors[$"{path}.Type"] = ["Feature type must be 'Feature'"];
            }

            if (feature.Geometry is null)
            {
                errors[$"{path}.Geometry"] = ["Geometry is required"];
            }
            else
            {
                if (feature.Geometry.Type is null || !GeometryTypes.Contains(feature.Geometry.Type))
                {
                    errors[$"{path}.Geometry.Type"] = [$"Geometry type must be one of: {string.Join(", ", GeometryTypes)}"];
                }

                if (feature.Geometry.Coordinates.ValueKind != JsonValueKind.Array)
                {
                    errors[$"{path}.Geometry.Coordinates"] = ["Coordinates must be an array"];
                }
            }

            if (feature.Properties is { } properties
                && properties.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
            {
                errors[$"{path}.Properties"] = ["Properties must be an object"];
            }
        }
    }
}
