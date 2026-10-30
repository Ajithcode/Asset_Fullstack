namespace A360.Repository.Settings;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("MongoDB connection string is required.");
        }

        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            throw new InvalidOperationException("MongoDB database name is required.");
        }
    }
}
