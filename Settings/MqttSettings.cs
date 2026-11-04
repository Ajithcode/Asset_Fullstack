namespace WorkerNetworkService.Settings;

public class MqttSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = Environment.MachineName;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseTls { get; set; }
    public bool CleanSession { get; set; } = true;
    public int KeepAliveSeconds { get; set; } = 60;
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>Redis stream each received message is added to, so the stream mappings store it in Mongo.</summary>
    public string TargetStream { get; set; } = "networkserver_stream";

    /// <summary>Stream entry field that holds the message payload; must match the mapping's PayloadField.</summary>
    public string PayloadField { get; set; } = "msg";

    public List<MqttSubscription> Subscriptions { get; set; } = [];
}

public class MqttSubscription
{
    public string Name { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Qos { get; set; } = 1;
}
