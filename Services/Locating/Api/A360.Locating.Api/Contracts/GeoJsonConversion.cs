using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using A360.Locating.Domain.Entities;

namespace A360.Locating.Api.Contracts;

internal static class GeoJsonConversion
{
    private static readonly JsonWriterSettings RelaxedJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    public static GeoJsonFeatureCollection ToEntity(GeoJsonFeatureCollectionContract? contract)
    {
        return new GeoJsonFeatureCollection
        {
            Type = contract?.Type?.Trim() ?? "FeatureCollection",
            Features = contract?.Features?.Select(feature => new GeoJsonFeature
            {
                Type = feature.Type?.Trim() ?? "Feature",
                Geometry = new GeoJsonGeometry
                {
                    Type = feature.Geometry?.Type?.Trim() ?? string.Empty,
                    Coordinates = feature.Geometry is { Coordinates.ValueKind: JsonValueKind.Array }
                        ? BsonSerializer.Deserialize<BsonArray>(feature.Geometry.Coordinates.GetRawText())
                        : []
                },
                Properties = feature.Properties is { ValueKind: JsonValueKind.Object } properties
                    ? BsonDocument.Parse(properties.GetRawText())
                    : []
            }).ToList() ?? []
        };
    }

    public static GeoJsonFeatureCollectionContract ToContract(GeoJsonFeatureCollection entity)
    {
        return new GeoJsonFeatureCollectionContract(
            entity.Type,
            entity.Features.Select(feature => new GeoJsonFeatureContract(
                feature.Type,
                new GeoJsonGeometryContract(feature.Geometry.Type, ToJsonElement(feature.Geometry.Coordinates)),
                ToJsonElement(feature.Properties))).ToList());
    }

    private static JsonElement ToJsonElement(BsonValue value)
    {
        return JsonDocument.Parse(value.ToJson(RelaxedJson)).RootElement.Clone();
    }
}
