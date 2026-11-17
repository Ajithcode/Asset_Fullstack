using System.Text.Json;
using System.Text.Json.Nodes;

namespace A360.Tracking.Api.Locating;

/// <summary>The two "find my tag" commands a tag accepts through its gateway.</summary>
public static class LocateCommandKind
{
    public const string Led = "led";
    public const string Buzzer = "buzzer";
}

/// <summary>
/// LED option (UI field -> jengine field):
/// Colour -> color (0-7, MBT02 only has 3 = red), On time (ms) -> light_on, Off time (ms) -> light_off,
/// Blink count -> cycles, Brightness -> brightness (1-100).
/// </summary>
public sealed record LedOptions(int Color, int OnMs, int OffMs, int Cycles, int Brightness)
{
    public static LedOptions From(JsonObject source) => new(
        Color: ReadInt(source, 3, "color", "colour"),
        OnMs: ReadInt(source, 300, "onMs", "light_on", "lightOn", "onTime"),
        OffMs: ReadInt(source, 300, "offMs", "light_off", "lightOff", "offTime"),
        Cycles: ReadInt(source, 10, "cycles", "blinkCount", "count"),
        Brightness: ReadInt(source, 100, "brightness"));

    public void Validate()
    {
        Require(Color, 0, 7, "Colour");
        Require(OnMs, 1, 65535, "On time (ms)");
        Require(OffMs, 1, 65535, "Off time (ms)");
        Require(Cycles, 1, 65535, "Blink count");
        Require(Brightness, 1, 100, "Brightness");
    }

    public void WriteTo(JsonObject payload)
    {
        payload["color"] = Color;
        payload["cycles"] = Cycles;
        payload["light_on"] = OnMs;
        payload["light_off"] = OffMs;
        payload["brightness"] = Brightness;
    }

    internal static int ReadInt(JsonObject source, int fallback, params string[] names) =>
        LocateCommandBuilder.ReadInt(source, fallback, names);

    internal static void Require(int value, int min, int max, string field) =>
        LocateCommandBuilder.Require(value, min, max, field);
}

/// <summary>
/// Buzzer option (UI field -> jengine field):
/// Beep time (ms) -> work (20-65535), Pause (ms) -> idle (20-65535), Beep count -> cycles (0 = off), Tone level -> level (0-5, 0 = default).
/// </summary>
public sealed record BuzzerOptions(int BeepMs, int PauseMs, int Cycles, int Level)
{
    public static BuzzerOptions From(JsonObject source) => new(
        BeepMs: LedOptions.ReadInt(source, 300, "beepMs", "work", "beepTime"),
        PauseMs: LedOptions.ReadInt(source, 300, "pauseMs", "idle", "pause"),
        Cycles: LedOptions.ReadInt(source, 5, "cycles", "beepCount", "count"),
        Level: LedOptions.ReadInt(source, 0, "level", "toneLevel", "tone"));

    public void Validate()
    {
        LedOptions.Require(BeepMs, 20, 65535, "Beep time (ms)");
        LedOptions.Require(PauseMs, 20, 65535, "Pause (ms)");
        LedOptions.Require(Cycles, 0, 65535, "Beep count");
        LedOptions.Require(Level, 0, 5, "Tone level");
    }

    public void WriteTo(JsonObject payload)
    {
        payload["work"] = BeepMs;
        payload["idle"] = PauseMs;
        payload["cycles"] = Cycles;
        payload["level"] = Level;
    }
}

/// <summary>A built command, ready to publish to /gw/{gateway}/action.</summary>
public sealed record LocateCommand(string Kind, int ReqId, string TagId, JsonObject Options, JsonObject Message);

/// <summary>Builds the jengine set_req message for an LED or buzzer command.</summary>
public static class LocateCommandBuilder
{
    private static int _nextReqId = Random.Shared.Next(1, 1_000_000);

    /// <summary>
    /// Reads the options of <paramref name="kind"/> from the front-end message: either nested
    /// ({"led":{...}} / {"buzzer":{...}}) or flat on the message itself.
    /// </summary>
    public static LocateCommand Build(string kind, string tagId, JsonObject message, LocatingSettings settings)
    {
        var source = message[kind] as JsonObject ?? message;
        var payload = new JsonObject
        {
            ["key"] = settings.CommandKey,
            ["single"] = true,
        };

        JsonObject options;
        switch (kind)
        {
            case LocateCommandKind.Led:
                var led = LedOptions.From(source);
                led.Validate();
                payload["opcode"] = settings.LedOpcode;
                led.WriteTo(payload);
                options = new JsonObject
                {
                    ["color"] = led.Color, ["onMs"] = led.OnMs, ["offMs"] = led.OffMs,
                    ["cycles"] = led.Cycles, ["brightness"] = led.Brightness,
                };
                break;

            case LocateCommandKind.Buzzer:
                if (settings.BuzzerOpcode is not { } buzzerOpcode)
                    throw new ArgumentException("Buzzer is not configured yet (Locating:BuzzerOpcode is empty).");
                var buzzer = BuzzerOptions.From(source);
                buzzer.Validate();
                payload["opcode"] = buzzerOpcode;
                buzzer.WriteTo(payload);
                options = new JsonObject
                {
                    ["beepMs"] = buzzer.BeepMs, ["pauseMs"] = buzzer.PauseMs,
                    ["cycles"] = buzzer.Cycles, ["level"] = buzzer.Level,
                };
                break;

            default:
                throw new ArgumentException($"Unknown command '{kind}'.");
        }

        payload["details"] = new JsonObject { [tagId] = new JsonObject() };

        var reqId = NextReqId();
        var command = new JsonObject
        {
            ["action"] = settings.CommandAction,
            ["version"] = 1,
            ["method"] = "set_req",
            ["req_id"] = reqId,
            ["payload"] = payload,
        };
        return new LocateCommand(kind, reqId, tagId, options, command);
    }

    // Stays positive and below 1,000,000 so it fits whatever the gateway uses for req_id.
    private static int NextReqId() => (int)((uint)Interlocked.Increment(ref _nextReqId) % 999_999) + 1;

    internal static int ReadInt(JsonObject source, int fallback, string[] names)
    {
        foreach (var name in names)
        {
            if (source[name] is not JsonValue value)
                continue;
            if (value.TryGetValue<int>(out var i))
                return i;
            if (value.TryGetValue<double>(out var d) && d == Math.Floor(d))
                return (int)d;
            if (value.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed))
                return parsed;
            throw new ArgumentException($"'{name}' must be a whole number.");
        }
        return fallback;
    }

    internal static void Require(int value, int min, int max, string field)
    {
        if (value < min || value > max)
            throw new ArgumentException($"{field} must be between {min} and {max} (got {value}).");
    }
}

/// <summary>
/// req_id / code / error of a gateway response. The gateway answers a command twice:
/// code 1 = accepted by the gateway, code 2 = done (error 0 = the tag is blinking / beeping, anything else = failed).
/// The fields are read from the top level first, then from "payload".
/// </summary>
public sealed record GatewayReply(int ReqId, int? Code, int? Error)
{
    public const string StatusSent = "sent";
    public const string StatusAccepted = "accepted";
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";
    public const string StatusTimeout = "timeout";

    public static GatewayReply? TryParse(string raw)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(raw) as JsonObject; }
        catch (JsonException) { return null; }
        if (root is null)
            return null;

        var inner = root["payload"] as JsonObject;
        var reqId = Number(root["req_id"]) ?? Number(inner?["req_id"]);
        if (reqId is null)
            return null;
        return new GatewayReply(reqId.Value,
            Number(root["code"]) ?? Number(inner?["code"]),
            Number(root["error"]) ?? Number(inner?["error"]));
    }

    /// <summary>The command status this reply moves to, or null when it says nothing about the outcome.</summary>
    public string? Status => Code switch
    {
        1 => StatusAccepted,
        2 => Error is null or 0 ? StatusCompleted : StatusFailed,
        _ => null,
    };

    private static int? Number(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<int>(out var i) => i,
        JsonValue v when v.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue => (int)l,
        JsonValue v when v.TryGetValue<double>(out var d) => (int)d,
        JsonValue v when v.TryGetValue<string>(out var s) && int.TryParse(s, out var p) => p,
        _ => null,
    };
}
