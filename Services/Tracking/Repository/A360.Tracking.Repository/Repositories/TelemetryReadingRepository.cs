using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class TelemetryReadingRepository
    : MongoRepository<TelemetryReading>, ITelemetryReadingRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "TelemetryReading";

    // Compares text ignoring case, for id lookups and device / sensor filters.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public TelemetryReadingRepository(IMongoDatabase database)
        : base(database.GetCollection<TelemetryReading>(CollectionName))
    {
    }

    public async Task<TelemetryReading?> GetByReadingIdAsync(string readingId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(reading => reading.ReadingId == readingId && !reading.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TelemetryReading>> QueryAsync(
        DateTime fromUtc, DateTime toUtc, string? deviceId, string? sensorId, int limit, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TelemetryReading>.Filter;
        var filters = new List<FilterDefinition<TelemetryReading>>
        {
            filter.Eq(reading => reading.IsDeleted, false),
            filter.Gte(reading => reading.RecordedAt, fromUtc),
            filter.Lt(reading => reading.RecordedAt, toUtc)
        };

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            filters.Add(filter.Eq(reading => reading.DeviceId, deviceId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(sensorId))
        {
            filters.Add(filter.Eq(reading => reading.SensorId, sensorId.Trim()));
        }

        return await Collection
            .Find(filter.And(filters), new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(reading => reading.RecordedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DailyCount>> CountPerDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return DailyCounts.CountAsync(Collection, "recorded_at", fromUtc, toUtc, cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TelemetryReading>(
                Builders<TelemetryReading>.IndexKeys.Ascending(reading => reading.ReadingId),
                new CreateIndexOptions { Name = "ix_telemetry_reading_reading_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<TelemetryReading>(
                Builders<TelemetryReading>.IndexKeys.Descending(reading => reading.RecordedAt),
                new CreateIndexOptions { Name = "ix_telemetry_reading_recorded_at" }),
            new CreateIndexModel<TelemetryReading>(
                Builders<TelemetryReading>.IndexKeys.Ascending(reading => reading.DeviceId).Descending(reading => reading.RecordedAt),
                new CreateIndexOptions { Name = "ix_telemetry_reading_device_recorded_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
