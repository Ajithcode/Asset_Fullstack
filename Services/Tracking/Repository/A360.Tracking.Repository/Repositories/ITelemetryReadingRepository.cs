using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ITelemetryReadingRepository : IMongoRepository<TelemetryReading>
{
    Task<TelemetryReading?> GetByReadingIdAsync(string readingId, CancellationToken cancellationToken = default);

    /// <summary>Readings in [from, to), newest first, optionally for one device / sensor; at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyCollection<TelemetryReading>> QueryAsync(
        DateTime fromUtc, DateTime toUtc, string? deviceId, string? sensorId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Readings per UTC day in [from, to).</summary>
    Task<IReadOnlyList<DailyCount>> CountPerDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
