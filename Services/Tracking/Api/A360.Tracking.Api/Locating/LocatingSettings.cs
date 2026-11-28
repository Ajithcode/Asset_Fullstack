namespace A360.Tracking.Api.Locating;

/// <summary>
/// "Locating" section of appsettings.json: the MQTT broker the gateways talk to and the collections used.
/// Username / Password are not committed; set them as Locating__Username / Locating__Password environment variables.
/// </summary>
public class LocatingSettings
{
    public const string SectionName = "Locating";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;

    /// <summary>Must differ from every other MQTT client on the broker, or the broker keeps kicking one of them off.</summary>
    public string ClientId { get; set; } = "a360-tracking-locating-" + Environment.MachineName;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseTls { get; set; }
    public int KeepAliveSeconds { get; set; } = 60;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int Qos { get; set; } = 1;

    /// <summary>Topic the gateway answers on; {gateway} is replaced by the gateway id.</summary>
    public string ResponseTopic { get; set; } = "/gw/{gateway}/response";

    /// <summary>Topic actions for the gateway are published to; {gateway} is replaced by the gateway id.</summary>
    public string ActionTopic { get; set; } = "/gw/{gateway}/action";

    /// <summary>Time-series collection the latest gateway of a tag is read from (meta.tagid, meta.gatewayid, timestamp).</summary>
    public string TimeSeriesCollection { get; set; } = "NetworkTimeSeries";

    /// <summary>
    /// Ids of the real gateways that may be commanded (repeaters relay readings but do not accept commands). When set, a command goes to the
    /// strongest DIRECT reading of one of these gateways within GatewayWindowSeconds; a tag not heard directly by any of them is refused
    /// ("out of the gateway's command range"). Empty = old behaviour (any device that reported the tag).
    /// </summary>
    public string[] CommandGatewayIds { get; set; } = [];

    /// <summary>Readings this close to a tag's newest one compete on RSSI to pick the gateway.</summary>
    public int GatewayWindowSeconds { get; set; } = 30;

    /// <summary>Collection every message received on a gateway's response topic is stored in.</summary>
    public string ResponseCollection { get; set; } = "GatewayResponses";

    /// <summary>Collection every LED / buzzer command and its outcome is stored in.</summary>
    public string CommandCollection { get; set; } = "TagLocateCommands";

    /// <summary>jengine "action" number of the LED entry in "cmds".</summary>
    public int LedAction { get; set; } = 42;

    /// <summary>jengine "action" number of the buzzer entry in "cmds".</summary>
    public int BuzzerAction { get; set; } = 56;

    /// <summary>jengine payload "key" sent with every command.</summary>
    public string CommandKey { get; set; } = "3141592653589793";

    /// <summary>Opcode of the LED blink command.</summary>
    public int LedOpcode { get; set; } = 90003;

    /// <summary>Opcode of the buzzer command.</summary>
    public int BuzzerOpcode { get; set; } = 90003;

    /// <summary>How long to wait for the gateway's final answer (code 2) before reporting a timeout.</summary>
    public int CommandTimeoutSeconds { get; set; } = 90;

    public string ResponseTopicFor(string gatewayId) => ResponseTopic.Replace("{gateway}", gatewayId);
    public string ActionTopicFor(string gatewayId) => ActionTopic.Replace("{gateway}", gatewayId);
}
