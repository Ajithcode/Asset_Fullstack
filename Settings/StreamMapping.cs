namespace WorkerNetworkService.Settings;

/// <summary>
/// Maps one Redis stream to one MongoDB time-series collection.
/// The payload is a JSON array: a header object (the gateway) followed by one object per tag,
/// and each tag becomes one document. Field lookups (Meta / Fields / TimestampFields) check the
/// tag object first, then the header object, then the stream entry's fields.
/// Values that are missing or null are left out of the document instead of being stored as null.
/// </summary>
public class StreamMapping
{
    public string Stream { get; set; } = "";
    public string Collection { get; set; } = "";
    public string Group { get; set; } = "timeseries-writer";

    /// <summary>Where a newly created group starts: "0" backfills the whole stream, "$" takes only new entries.</summary>
    public string StartId { get; set; } = "0";

    /// <summary>Entry field holding the JSON payload array.</summary>
    public string PayloadField { get; set; } = "msg";

    /// <summary>Candidate fields for the time field, first match wins. Falls back to the stream entry id's time.</summary>
    public List<string> TimestampFields { get; set; } = [];

    /// <summary>
    /// meta key -> source field name. Meta should hold values that identify a series (client, gateway, tag).
    /// Configuration binding orders dictionary keys alphabetically, so meta keys are written in that order.
    /// </summary>
    public Dictionary<string, string> Meta { get; set; } = [];

    /// <summary>Document fields, written in list order after timestamp and meta.</summary>
    public List<FieldMapping> Fields { get; set; } = [];

    /// <summary>
    /// Document field that collects the tag's fields not used by Meta, Fields or TimestampFields
    /// (e.g. temperature); left out when there are none. Null = don't collect them.
    /// </summary>
    public string? AttributesField { get; set; }

    public string Granularity { get; set; } = "seconds";
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// XDEL entries from the stream once they are stored and acked. Only enable when this is the
    /// stream's only consumer group, since deleted entries are gone for every other group too.
    /// </summary>
    public bool DeleteAfterAck { get; set; }
}

/// <summary>
/// One document field. Set exactly one of:
///   Source                 copy the source field as-is;
///   Longitude + Latitude   build a GeoJSON Point (left out unless both are numbers);
///   Object                 build a sub-document, sub-key -> source field (left out when all are missing).
/// </summary>
public class FieldMapping
{
    public string Name { get; set; } = "";
    public string? Source { get; set; }
    public string? Longitude { get; set; }
    public string? Latitude { get; set; }
    public Dictionary<string, string>? Object { get; set; }

    public IEnumerable<string> SourceFields =>
        new[] { Source, Longitude, Latitude }.Concat(Object?.Values ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrEmpty(s)).Select(s => s!);
}
