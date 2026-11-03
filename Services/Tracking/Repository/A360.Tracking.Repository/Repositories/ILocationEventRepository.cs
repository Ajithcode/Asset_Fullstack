using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ILocationEventRepository : IMongoRepository<LocationEvent>
{
    Task<LocationEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>Events per UTC day and event type in [from, to).</summary>
    Task<IReadOnlyList<DailyCount>> CountPerDayByTypeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
