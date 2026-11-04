using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkerService.Models;

// Some devices report normally-string fields (e.g. healthy_state) as JSON numbers.
public class StringOrNumberConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert token type '{reader.TokenType}' to string")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
