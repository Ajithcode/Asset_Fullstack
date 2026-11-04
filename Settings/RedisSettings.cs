namespace WorkerNetworkService.Settings;

public class RedisSettings
{
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Consumer name inside each group. Keep it stable across restarts so un-acked entries are retried.</summary>
    public string ConsumerName { get; set; } = Environment.MachineName;
}
