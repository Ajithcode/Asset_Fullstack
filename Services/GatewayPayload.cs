using System.Text.Json.Nodes;

namespace NetworkServer.Services;

/// <summary>
/// A parsed BLE gateway message. Kind is the incoming layout ("report" or "gateway"); Tags are already in the
/// stored layout {tagid, rssi, raw, batterypercentage, device_trigger, status, time}.
/// </summary>
public record GatewayBatch(string Kind, string GatewayId, List<JsonObject> Tags);

/// <summary>
/// Recognises the BLE gateway payloads:
///   report:  {"report":{"apBlock":{"serial":..,"mac":"c8:51:fb:f7:2b:00"},"bleScanReport":[{"mac":"c3:00:00:99:13:8f","rssi":-89,"payload":"&lt;base64&gt;",..}]}}
///   gateway: [{"gateway":"ac233fc28e11","timestamp":..,"seq":..},{"mac":"c3000071fe4c","rssi":-54,"raw":"&lt;hex&gt;",..}, ...]
///            [{"gatewayid":"c851fbf72b00","timestamp":..},{"tagid":"c3000099138f","rssi":-83,"raw":"&lt;base64&gt;",..}, ...]
/// and converts every tag entry to the stored layout:
///   {"tagid":"c3000099138f","rssi":-83,"raw":"&lt;base64&gt;","batterypercentage":null,"device_trigger":null,"status":"online"}
/// </summary>
public static class GatewayPayload
{
    public static bool TryParse(JsonNode? data, out GatewayBatch batch)
    {
        batch = null!;

        if (data is JsonObject root && root["report"] is JsonObject report)
        {
            var apBlock = report["apBlock"] as JsonObject;
            var gatewayId = NormalizeMac(Str(apBlock?["mac"]) ?? Str(apBlock?["serial"]));
            if (gatewayId is null)
                return false;

            var scans = report["bleScanReport"] as JsonArray ?? [];
            batch = new GatewayBatch("report", gatewayId, Tags(scans));
            return true;
        }

        if (data is JsonArray { Count: > 0 } array && array[0] is JsonObject header)
        {
            var gatewayId = NormalizeMac(Str(header["gatewayid"]) ?? Str(header["gateway"]));
            if (gatewayId is null)
                return false;

            batch = new GatewayBatch("gateway", gatewayId, Tags(array.Skip(1)));
            return true;
        }

        return false;
    }

    public static string TagId(JsonObject tag) => (string)tag["tagid"]!;

    // Higher RSSI is closer (-54 beats -60); a tag without RSSI never wins over one with it.
    // Parsed JSON reads as any number type, a value created in code (relayed tags) only as its own type.
    public static double Rssi(JsonObject tag) => tag["rssi"] switch
    {
        JsonValue v when v.TryGetValue<double>(out var rssi) => rssi,
        JsonValue v when v.TryGetValue<int>(out var rssi) => rssi,
        _ => double.MinValue,
    };

    private static List<JsonObject> Tags(IEnumerable<JsonNode?> items)
    {
        var tags = new List<JsonObject>();
        foreach (var item in items.OfType<JsonObject>())
        {
            var tagId = NormalizeMac(Str(item["tagid"]) ?? Str(item["mac"]));
            if (tagId is null)
                continue;

            // Fields the gateway doesn't send are stored as null.
            var raw = ToBase64(Str(item["raw"]) ?? Str(item["payload"]));
            tags.Add(new JsonObject
            {
                ["tagid"] = tagId,
                ["rssi"] = item["rssi"]?.DeepClone(),
                ["raw"] = raw,
                ["batterypercentage"] = item["batterypercentage"]?.DeepClone() ?? BatteryFromRaw(raw),
                ["device_trigger"] = Str(item["device_trigger"]),
                ["status"] = Str(item["status"]) ?? "online", // it was just seen by the gateway
                ["time"] = Time(item["time"] ?? item["timestamp"]),
            });
        }
        return tags;
    }

    // Scan time sent by the gateway: Unix seconds / milliseconds, or an ISO string ("2026-11-27T09:38:23.529Z") ->
    // "2026-11-23T05:31:18.000Z"; null if absent or empty.
    private static string? Time(JsonNode? node)
    {
        if (node is not JsonValue v)
            return null;
        long ms;
        if (v.TryGetValue<string>(out var text))
        {
            if (!DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                return null;
            ms = parsed.ToUnixTimeMilliseconds();
        }
        else if (v.TryGetValue<double>(out var t) && t > 0)
            ms = t > 1e12 ? (long)t : (long)(t * 1000);
        else
            return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
    }

    // "c3:00:00:9a:af:45" -> "c300009aaf45"
    private static string? NormalizeMac(string? mac) => mac?.Replace(":", "").Trim().ToLowerInvariant();

    // Some gateways send the advertisement as hex, others as base64; store it as base64 either way.
    private static string? ToBase64(string? raw) =>
        raw is { Length: > 0 } && raw.Length % 2 == 0 && raw.All(Uri.IsHexDigit)
            ? Convert.ToBase64String(Convert.FromHexString(raw))
            : raw;

    public static int? Battery(JsonObject tag) =>
        tag["batterypercentage"] is JsonValue v && v.TryGetValue<int>(out var battery) ? battery : null;

    /// <summary>
    /// Reads the battery percentage from the advertisement (base64), for the frames that carry one:
    ///   Minew 0xFFE1 service data:          16 E1 FF A1 &lt;version&gt; &lt;battery&gt; ...
    ///   Minew 0xFFF1 iBeacon + battery:     16 .. &lt;uuid&gt; &lt;major&gt; &lt;minor&gt; &lt;txpower&gt; &lt;battery&gt;
    ///   iBeacon with a 22-byte body:        FF 4C 00 02 16 &lt;uuid&gt; &lt;major&gt; &lt;minor&gt; &lt;txpower&gt; &lt;battery&gt;
    /// Other frames (plain iBeacon, Eddystone UID/URL/TLM, ...) have no percentage and give null.
    /// </summary>
    private static int? BatteryFromRaw(string? raw)
    {
        var buffer = new byte[raw?.Length ?? 0];
        if (raw is null || !Convert.TryFromBase64String(raw, buffer, out var length))
            return null;
        var adv = buffer.AsSpan(0, length);

        bool hasFff1 = false;
        int? battery = null;
        // Advertisement is a list of AD structures: <length> <type> <data...>
        for (var i = 0; i + 1 < adv.Length && adv[i] > 0 && i + adv[i] < adv.Length; i += adv[i] + 1)
        {
            var type = adv[i + 1];
            var data = adv.Slice(i + 2, adv[i] - 1);

            if (type is 0x02 or 0x03 && data.Length >= 2 && data[0] == 0xF1 && data[1] == 0xFF)
                hasFff1 = true;
            else if (type == 0x16 && data.Length >= 5 && data[0] == 0xE1 && data[1] == 0xFF && data[2] == 0xA1)
                battery = data[4];
            else if (type == 0x16 && hasFff1 && data.Length == 22)
                battery = data[^1];
            else if (type == 0xFF && data.Length == 26 && data[0] == 0x4C && data[1] == 0x00 && data[2] == 0x02 && data[3] == 0x16)
                battery = data[^1];
        }
        return battery is <= 100 ? battery : null;
    }

    private static string? Str(JsonNode? node)
    {
        var s = node is JsonValue v && v.TryGetValue<string>(out var str) ? str : node?.ToJsonString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
