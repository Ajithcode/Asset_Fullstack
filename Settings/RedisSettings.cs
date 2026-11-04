namespace WorkerService.Settings
{
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string StreamName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string ConsumerName { get; set; } = string.Empty;
    }
}