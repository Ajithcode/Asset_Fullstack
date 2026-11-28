using System.Text.Json;
using System.Text.Json.Nodes;

namespace A360.Tracking.Api.Locating;

/// <summary>The "find my tag" commands a tag accepts through its gateway.</summary>
public static class LocateCommandKind
{
    public const string Led = "led";
    public const string Buzzer = "buzzer";

    /// <summary>LED and buzzer together, sent as one message with both entries in "cmds".</summary>
    public const string Both = "both";

    /// <summary>Turns the tag's LED and buzzer off: the same message as Both, with LED color 0 and buzzer cycles 0.</summary>
    public const string Off = "off";

    /// <summary>The kind a front-end message type stands for, or null when it is not a command.</summary>
    public static string? FromType(string? type) => type switch
    {
        Led => Led,
        Buzzer => Buzzer,
        Both or "locate" or "ledbuzzer" or "led+buzzer" or "led_buzzer" => Both,
        Off or "turnoff" or "turn_off" or "stop" => Off,
        _ => null,
    };
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

/// <summary>
/// Builds the jengine message for an LED, buzzer or LED + buzzer command:
/// {"version":2,"req_id":503,"cmds":[{"action":42,"method":"set_req","payload":{LED}},{"action":56,"method":"set_req","payload":{buzzer}}]}
/// Off sends the same two entries with LED color 0 and buzzer cycles 0.
/// </summary>
public static class LocateCommandBuilder
{
    private static int _nextReqId = Random.Shared.Next(1, 1_000_000);

    /// <summary>
    /// Reads the options from the front-end message: nested ({"led":{...}} / {"buzzer":{...}}) or flat on the message itself.
    /// For both, a flat message gives the same values (e.g. "cycles") to the LED and the buzzer.
    /// </summary>
    public static LocateCommand Build(string kind, string tagId, JsonObject message, LocatingSettings settings)
    {
        var cmds = new JsonArray();
        var options = new JsonObject();

        if (kind is not (LocateCommandKind.Led or LocateCommandKind.Buzzer or LocateCommandKind.Both or LocateCommandKind.Off))
            throw new ArgumentException($"Unknown command '{kind}'.");

        var off = kind == LocateCommandKind.Off;
        var pair = kind is LocateCommandKind.Both or LocateCommandKind.Off;

        if (kind is LocateCommandKind.Led || pair)
        {
            var led = LedOptions.From(message[LocateCommandKind.Led] as JsonObject ?? message);
            if (off)
                led = led with { Color = 0 };
            led.Validate();
            var payload = NewPayload(settings.LedOpcode, settings);
            led.WriteTo(payload);
            cmds.Add(Cmd(settings.LedAction, payload, tagId));
            var ledOptions = new JsonObject
            {
                ["color"] = led.Color, ["onMs"] = led.OnMs, ["offMs"] = led.OffMs,
                ["cycles"] = led.Cycles, ["brightness"] = led.Brightness,
            };
            if (pair) options[LocateCommandKind.Led] = ledOptions;
            else options = ledOptions;
        }

        if (kind is LocateCommandKind.Buzzer || pair)
        {
            var buzzer = BuzzerOptions.From(message[LocateCommandKind.Buzzer] as JsonObject ?? message);
            if (off)
                buzzer = buzzer with { Cycles = 0 };
            buzzer.Validate();
            var payload = NewPayload(settings.BuzzerOpcode, settings);
            buzzer.WriteTo(payload);
            cmds.Add(Cmd(settings.BuzzerAction, payload, tagId));
            var buzzerOptions = new JsonObject
            {
                ["beepMs"] = buzzer.BeepMs, ["pauseMs"] = buzzer.PauseMs,
                ["cycles"] = buzzer.Cycles, ["level"] = buzzer.Level,
            };
            if (pair) options[LocateCommandKind.Buzzer] = buzzerOptions;
            else options = buzzerOptions;
        }

        var reqId = NextReqId();
        var command = new JsonObject
        {
            ["version"] = 2,
            ["req_id"] = reqId,
            ["cmds"] = cmds,
        };
        return new LocateCommand(kind, reqId, tagId, options, command);
    }

    private static JsonObject NewPayload(int opcode, LocatingSettings settings) => new()
    {
        ["key"] = settings.CommandKey,
        ["opcode"] = opcode,
        ["single"] = true,
    };

    // "details" goes last, after the LED / buzzer fields.
    private static JsonObject Cmd(int action, JsonObject payload, string tagId)
    {
        payload["details"] = new JsonObject { [tagId] = new JsonObject() };
        return new JsonObject { ["action"] = action, ["method"] = "set_req", ["payload"] = payload };
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
public sealed record GatewayReply(int ReqId, int? Code, int? Error, string? Message = null)
{
    public const string StatusSent = "sent";
    public const string StatusAccepted = "accepted";
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";
    public const string StatusTimeout = "timeout";

    /// <summary>
    /// Reads a gateway reply in any of its shapes: {"req_id","payload":{"code"}} (v1), {"req_id","cmds":[{"payload":{"code"}}]} (v2, code 1 =
    /// accepted) and the final per-tag result {"payload":{"details":{"&lt;tag&gt;":{"code":2,"error":0,"message":"..."}}}} (code 2 = sent to the tag,
    /// any other code = the gateway's error, e.g. 671). With several cmds (LED + buzzer) any failure wins, completed needs all of them.
    /// </summary>
    public static GatewayReply? TryParse(string raw, string? tagId = null)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(raw) as JsonObject; }
        catch (JsonException) { return null; }
        if (root is null)
            return null;

        var results = new List<(int? Code, int? Error, string? Message)>();
        void Read(JsonObject? payload)
        {
            if (payload is null) return;
            if (payload["details"] is JsonObject details)
            {
                var entry = (tagId != null && details[tagId] is JsonObject own ? own : details.Select(kv => kv.Value as JsonObject).FirstOrDefault(o => o != null));
                if (entry != null) results.Add((Number(entry["code"]), Number(entry["error"]), (entry["message"] as JsonValue)?.ToString()));
                return;
            }
            results.Add((Number(payload["code"]), Number(payload["error"]), (payload["message"] as JsonValue)?.ToString()));
        }

        var inner = root["payload"] as JsonObject;
        var reqId = Number(root["req_id"]) ?? Number(inner?["req_id"]);
        if (reqId is null)
            return null;

        if (root["cmds"] is JsonArray cmds)
            foreach (var cmd in cmds)
                Read((cmd as JsonObject)?["payload"] as JsonObject);
        else if (root["code"] is not null)
            results.Add((Number(root["code"]), Number(root["error"]), (root["message"] as JsonValue)?.ToString()));
        else
            Read(inner);

        var known = results.Where(r => r.Code.HasValue).ToList();
        if (known.Count == 0)
            return new GatewayReply(reqId.Value, null, Number(root["error"]) ?? Number(inner?["error"]));

        // a failure wins: any code other than 1 / 2, or code 2 with a non-zero error
        var bad = known.FirstOrDefault(r => (r.Code != 1 && r.Code != 2) || (r.Code == 2 && r.Error is not (null or 0)));
        if (bad.Code.HasValue)
            return new GatewayReply(reqId.Value, bad.Code, bad.Error ?? 0, bad.Message);
        var allDone = known.All(r => r.Code == 2);
        var pick = known[0];
        return allDone
            ? new GatewayReply(reqId.Value, 2, 0, known.Select(r => r.Message).FirstOrDefault(m => !string.IsNullOrEmpty(m)))
            : new GatewayReply(reqId.Value, 1, null, pick.Message);
    }

    public string? Status => Code switch
    {
        1 => StatusAccepted,
        2 => Error is null or 0 ? StatusCompleted : StatusFailed,
        null => null,
        _ => StatusFailed,
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
