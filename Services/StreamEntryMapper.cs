using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Converts a Redis stream entry into one time-series document per tag:
///   { _id, timestamp, meta: { ...mapping.Meta }, ...mapping.Fields, [AttributesField]: { ...other tag fields } }
/// Missing or null values are left out rather than stored as null.
/// The payload is [ header, tag, tag, ... ]; an entry with only a header yields no documents.
/// </summary>
public static class StreamEntryMapper
{
    public static List<BsonDocument> ToDocuments(StreamEntry entry, StreamMapping mapping)
    {
        var fields = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());

        if (!fields.TryGetValue(mapping.PayloadField, out var raw))
            throw new FormatException($"Entry has no '{mapping.PayloadField}' field.");
        if (BsonSerializer.Deserialize<BsonValue>(raw) is not BsonArray { Count: > 0 } items || !items[0].IsBsonDocument)
            throw new FormatException("Payload is not a JSON array starting with a header object.");

        var header = items[0].AsBsonDocument;
        var used = mapping.Meta.Values.Concat(mapping.Fields.SelectMany(f => f.SourceFields))
            .Concat(mapping.TimestampFields).ToHashSet();

        return items.Skip(1)
            .Where(item => item.IsBsonDocument)
            .Select(item => ToDocument(entry, mapping, fields, header, item.AsBsonDocument, used))
            .ToList();
    }

    private static BsonDocument ToDocument(StreamEntry entry, StreamMapping mapping,
        Dictionary<string, string> fields, BsonDocument header, BsonDocument tag, HashSet<string> used)
    {
        BsonValue Lookup(string name) =>
            tag.TryGetValue(name, out var t) ? t
            : header.TryGetValue(name, out var h) ? h
            : fields.TryGetValue(name, out var f) ? ScalarValue(f)
            : BsonNull.Value;

        var meta = new BsonDocument();
        foreach (var (metaKey, sourceField) in mapping.Meta)
            AddIfPresent(meta, metaKey, Lookup(sourceField));

        var timestamp = mapping.TimestampFields
            .Select(name => ParseTimestamp(Lookup(name)))
            .FirstOrDefault(t => t != null)
            ?? EntryIdTime(entry.Id);

        var doc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "timestamp", timestamp },
        };
        AddIfPresent(doc, "meta", meta);
        foreach (var field in mapping.Fields)
        {
            var value = FieldValue(field, Lookup);
            if (field.Array)
                doc[field.Name] = value switch
                {
                    BsonArray array => array,
                    BsonNull => new BsonArray(),
                    _ => new BsonArray { value },
                };
            else
                AddIfPresent(doc, field.Name, value);
        }

        if (mapping.AttributesField is { } attributesField)
        {
            var attributes = new BsonDocument();
            foreach (var e in tag.Where(e => !used.Contains(e.Name)))
                AddIfPresent(attributes, e.Name, e.Value);
            AddIfPresent(doc, attributesField, attributes);
        }

        return doc;
    }

    private static BsonValue FieldValue(FieldMapping field, Func<string, BsonValue> lookup)
    {
        if (field.Source != null)
            return lookup(field.Source);

        if (field.Longitude != null && field.Latitude != null)
        {
            var lon = lookup(field.Longitude);
            var lat = lookup(field.Latitude);
            return lon.IsNumeric && lat.IsNumeric
                ? new BsonDocument { { "type", "Point" }, { "coordinates", new BsonArray { lon, lat } } }
                : BsonNull.Value;
        }

        if (field.Object != null)
        {
            var obj = new BsonDocument();
            foreach (var (key, sourceField) in field.Object)
                AddIfPresent(obj, key, lookup(sourceField));
            return obj;
        }

        throw new InvalidOperationException($"Field '{field.Name}' needs Source, Longitude + Latitude, or Object.");
    }

    // Leaves out nulls and empty sub-documents so absent values don't show up as null fields.
    private static void AddIfPresent(BsonDocument doc, string name, BsonValue value)
    {
        if (value.IsBsonNull || value is BsonDocument { ElementCount: 0 })
            return;
        doc[name] = value;
    }

    // Only convert when the number round-trips exactly, so ids like "000601" stay strings.
    private static BsonValue ScalarValue(string value) =>
        long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)
            && l.ToString(CultureInfo.InvariantCulture) == value ? l
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d.ToString("R", CultureInfo.InvariantCulture) == value ? d
        : value;

    private static DateTime? ParseTimestamp(BsonValue value) =>
        value.IsValidDateTime ? value.ToUniversalTime()
        : value.IsString && DateTime.TryParse(value.AsString, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t
        : null;

    private static DateTime EntryIdTime(RedisValue id)
    {
        var s = id.ToString();
        var ms = long.Parse(s.AsSpan(0, s.IndexOf('-')), CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
    }
}
