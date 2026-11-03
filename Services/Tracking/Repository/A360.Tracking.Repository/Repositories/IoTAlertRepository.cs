using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class IoTAlertRepository
    : MongoRepository<IoTAlert>, IIoTAlertRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "IoTAlert";

    // Compares text ignoring case, for alert id lookups and filters.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public IoTAlertRepository(IMongoDatabase database)
        : base(database.GetCollection<IoTAlert>(CollectionName))
    {
    }

    public async Task<IoTAlert?> GetByAlertIdAsync(string alertId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(alert => alert.AlertId == alertId && !alert.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<IoTAlert>> QueryAsync(IoTAlertQuery query, CancellationToken cancellationToken = default)
    {
        var filter = Builders<IoTAlert>.Filter;
        var filters = new List<FilterDefinition<IoTAlert>> { filter.Eq(alert => alert.IsDeleted, false) };

        if (query.FromUtc is not null)
        {
            filters.Add(filter.Gte(alert => alert.RaisedAt, query.FromUtc.Value));
        }

        if (query.ToUtc is not null)
        {
            filters.Add(filter.Lt(alert => alert.RaisedAt, query.ToUtc.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(filter.Eq(alert => alert.Status, query.Status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            filters.Add(filter.Eq(alert => alert.Severity, query.Severity.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.SiteName))
        {
            filters.Add(filter.Eq(alert => alert.SiteName, query.SiteName.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            filters.Add(filter.Eq(alert => alert.AssetRegistrationId, query.AssetRegistrationId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(filter.Or(
                filter.Regex(alert => alert.AlertId, pattern),
                filter.Regex(alert => alert.AssetName, pattern),
                filter.Regex(alert => alert.AssetRegistrationId, pattern),
                filter.Regex(alert => alert.DeviceId, pattern),
                filter.Regex(alert => alert.EventType, pattern),
                filter.Regex(alert => alert.Message, pattern)));
        }

        return await Collection
            .Find(filter.And(filters), new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(alert => alert.RaisedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DailyCount>> CountPerDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return DailyCounts.CountAsync(Collection, "raised_at", fromUtc, toUtc, cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<IoTAlert>(
                Builders<IoTAlert>.IndexKeys.Ascending(alert => alert.AlertId),
                new CreateIndexOptions { Name = "ix_iot_alert_alert_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<IoTAlert>(
                Builders<IoTAlert>.IndexKeys.Descending(alert => alert.RaisedAt),
                new CreateIndexOptions { Name = "ix_iot_alert_raised_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
