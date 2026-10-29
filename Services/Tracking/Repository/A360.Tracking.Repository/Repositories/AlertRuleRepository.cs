using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class AlertRuleRepository
    : MongoRepository<AlertRule>, IAlertRuleRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AlertRule";

    // Compares text ignoring case, for alert rule name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AlertRuleRepository(IMongoDatabase database)
        : base(database.GetCollection<AlertRule>(CollectionName))
    {
    }

    public async Task<bool> NameExistsAsync(string geofenceId, string name, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(rule => rule.GeofenceId == geofenceId && rule.Name == name, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AlertRule>(
                Builders<AlertRule>.IndexKeys
                    .Ascending(rule => rule.GeofenceId)
                    .Ascending(rule => rule.Name),
                new CreateIndexOptions { Name = "ix_alert_rule_geofence_id_name", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
