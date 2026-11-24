namespace WorkerNetworkService.Settings;

/// <summary>
/// Maps one Redis stream to one MongoDB time-series collection.
/// The payload is a JSON array: a header object (the gateway) followed by one object per tag,
/// and each tag becomes one document. Field lookups (Meta / Fields / TimestampFields) check the
/// tag object first, then the header object, then the stream entry's fields.
/// Fields that are missing are stored as null; missing meta values are left out.
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

    /// <summary>
    /// Meta keys every document must have (e.g. gatewayid, tagid). A tag missing any of them is skipped and logged
    /// instead of being stored, so payloads in another layout don't end up in the collection.
    /// </summary>
    public List<string> RequiredMeta { get; set; } = [];

    /// <summary>Document fields, written in list order after timestamp and meta.</summary>
    public List<FieldMapping> Fields { get; set; } = [];

    /// <summary>
    /// Document field that collects the tag's fields not used by Meta, Fields or TimestampFields
    /// (e.g. temperature); left out when there are none. Null = don't collect them.
    /// </summary>
    public string? AttributesField { get; set; }

    /// <summary>
    /// Document field that gets "in" / "out" computed from each tag's previous gateway (see DeviceTriggerTracker),
    /// replacing any value from the payload. Needs gatewayid and tagid in Meta. Null = off.
    /// </summary>
    public string? DeviceTriggerField { get; set; }

    /// <summary>Redis hash holding each tag's last gateway and trigger. Defaults to "{Stream}:device_trigger".</summary>
    public string? DeviceTriggerKey { get; set; }

    public string DeviceTriggerStateKey => DeviceTriggerKey ?? $"{Stream}:device_trigger";

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
///   Longitude + Latitude   build a GeoJSON Point (null unless both are numbers);
///   Object                 build a sub-document, sub-key -> source field (null when all are missing).
/// With Array = true the field is stored as an array: a source array is kept as-is, a single value is
/// wrapped in an array, and a missing value is stored as null. With Date = true the value is stored as a date.
/// </summary>
public class FieldMapping
{
    public string Name { get; set; } = "";
    public string? Source { get; set; }
    public string? Longitude { get; set; }
    public string? Latitude { get; set; }
    public Dictionary<string, string>? Object { get; set; }
    public bool Array { get; set; }

    /// <summary>Store the value as a date (ISO string or date); null when it isn't one.</summary>
    public bool Date { get; set; }

    public IEnumerable<string> SourceFields =>
        new[] { Source, Longitude, Latitude }.Concat(Object?.Values ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrEmpty(s)).Select(s => s!);
}
