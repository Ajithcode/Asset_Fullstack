using MongoDB.Driver;
using A360.Repository.Repositories;
using SeverityEntity = A360.Inspection.Domain.Entities.Severity;

namespace A360.Inspection.Repository.Repositories;

public sealed class SeverityRepository : MongoRepository<SeverityEntity>, ISeverityRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "severities";

    public SeverityRepository(IMongoDatabase database)
        : base(database.GetCollection<SeverityEntity>(CollectionName))
    {
    }

    public async Task<SeverityEntity?> GetBySeverityCodeAsync(string severityCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(severity => severity.SeverityCode == severityCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SeverityEntity>(
                Builders<SeverityEntity>.IndexKeys.Ascending(severity => severity.SeverityCode),
                new CreateIndexOptions { Name = "ix_severities_severity_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
