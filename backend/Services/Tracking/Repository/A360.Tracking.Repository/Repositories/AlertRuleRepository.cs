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

    public async Task<bool> NameExistsAsync(string geofenceId, string name, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(rule => rule.GeofenceId == geofenceId && rule.Name == name && rule.Id != excludeId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyForGeofenceAsync(string geofenceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(rule => rule.GeofenceId == geofenceId && !rule.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    public async Task RenameGeofenceAsync(string geofenceId, string geofenceName, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            rule => rule.GeofenceId == geofenceId,
            Builders<AlertRule>.Update.Set(rule => rule.GeofenceName, geofenceName),
            cancellationToken: cancellationToken);
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
