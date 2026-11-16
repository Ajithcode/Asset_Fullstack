using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetworkServer.Services;

/// <summary>
/// Converts an incoming raw payload (ASCII text) into a JSON node.
/// Supported formats, tried in order:
///   0. JSON array of ASCII codes    [123,34,...]     -> decoded to text, then parsed again
///   1. Already-valid JSON           {"temp":23.5}
///   2. key=value / key:value pairs  temp=23.5,hum=40  or  temp:23.5;hum:40
///   3. Delimited values (CSV-like)  23.5,40,OK       -> ["23.5","40","OK"] typed
///   4. Anything else                stored as a plain string
/// </summary>
public static class AsciiPayloadParser
{
    private static readonly char[] PairSeparators = [',', ';', '&', '|', '\n'];

    public static (string Format, JsonNode? Data) Parse(byte[] payload)
    {
        // Strip non-printable chars (except tab/newline) that serial devices often append (\r, \0, etc.)
        var text = new string(Encoding.ASCII.GetString(payload)
            .Where(c => c == '\t' || c == '\n' || (c >= 0x20 && c < 0x7F))
            .ToArray()).Trim();

        return ParseText(text);
    }

    private static (string Format, JsonNode? Data) ParseText(string text)
    {
        if (text.Length == 0)
            return ("empty", null);

        if (text[0] is '{' or '[')
        {
            JsonNode? json = null;
            try { json = JsonNode.Parse(text); }
            catch (JsonException) { /* not JSON, fall through */ }

            if (json is not null)
            {
                // Devices sometimes send the message as a list of character codes: [123,34,114,...] = {"r...
                if (TryDecodeByteArray(json, out var decoded))
                {
                    var (innerFormat, innerData) = ParseText(decoded);
                    // Looks like JSON but doesn't parse (e.g. a cut-off message): keep the decoded text as-is
                    if (decoded.Length > 0 && decoded[0] is '{' or '[' && !innerFormat.Contains("json"))
                        return ("ascii-codes:incomplete-json", JsonValue.Create(decoded));
                    return ($"ascii-codes:{innerFormat}", innerData);
                }
                return ("json", json);
            }
        }

        var parts = text.Split(PairSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.All(p => p.IndexOfAny(['=', ':']) > 0))
        {
            var obj = new JsonObject();
            foreach (var part in parts)
            {
                var idx = part.IndexOfAny(['=', ':']);
                obj[part[..idx].Trim()] = ToTypedValue(part[(idx + 1)..].Trim());
            }
            return ("key-value", obj);
        }

        if (parts.Length > 1)
            return ("delimited", new JsonArray(parts.Select(ToTypedValue).ToArray()));

        return ("text", ToTypedValue(text));
    }

    private static bool TryDecodeByteArray(JsonNode node, out string text)
    {
        text = "";
        if (node is not JsonArray array || array.Count < 2)
            return false;

        var bytes = new byte[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonValue v || !v.TryGetValue<int>(out var b) || b < 0 || b > 255)
                return false;
            bytes[i] = (byte)b;
        }

        text = Encoding.UTF8.GetString(bytes).Trim('\0', '\r', '\n', ' ');
        return true;
    }

    private static JsonNode? ToTypedValue(string value)
    {
        if (long.TryParse(value, out var l)) return JsonValue.Create(l);
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
        if (bool.TryParse(value, out var b)) return JsonValue.Create(b);
        return JsonValue.Create(value);
    }
}
