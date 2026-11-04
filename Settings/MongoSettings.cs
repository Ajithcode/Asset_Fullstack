namespace WorkerNetworkService.Settings;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?directConnection=true";
    public string Database { get; set; } = "Asset360";
}
