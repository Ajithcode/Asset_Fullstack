using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

/// <summary>Filters for the alert list and count. Empty values mean "all"; text is matched ignoring case.</summary>
public sealed record IoTAlertQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Status = null,
    string? Severity = null,
    string? SiteName = null,
    string? AssetRegistrationId = null,
    string? Search = null);

public interface IIoTAlertRepository : IMongoRepository<IoTAlert>
{
    Task<IoTAlert?> GetByAlertIdAsync(string alertId, CancellationToken cancellationToken = default);

    /// <summary>Matching alerts, most recently raised first.</summary>
    Task<IReadOnlyCollection<IoTAlert>> QueryAsync(IoTAlertQuery query, CancellationToken cancellationToken = default);

    /// <summary>Alerts raised per UTC day in [from, to).</summary>
    Task<IReadOnlyList<DailyCount>> CountPerDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
