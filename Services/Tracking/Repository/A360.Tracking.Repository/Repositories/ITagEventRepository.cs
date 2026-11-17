using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

/// <summary>Filters of the Audit Trail tab. Every field is optional; <see cref="Page"/> is 1-based.</summary>
public sealed record TagEventQuery(
    string? Search,
    string? EventType,
    string? Technology,
    string? PerformedBy,
    string? TagId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public interface ITagEventRepository : IMongoRepository<TagEvent>
{
    /// <summary>One page of events matching the query, newest first, and how many match in all.</summary>
    Task<(IReadOnlyList<TagEvent> Items, long Total)> QueryAsync(TagEventQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every event of one tag, newest first.</summary>
    Task<IReadOnlyList<TagEvent>> GetByTagIdAsync(string tagId, CancellationToken cancellationToken = default);

    /// <summary>Everyone who has performed an event, for the Performed By dropdown.</summary>
    Task<IReadOnlyList<string>> GetPerformersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// For each tag with an event before <paramref name="beforeUtc"/>, its status after the last such event
    /// (null when it had been deleted): the tag's status at that moment.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetStatusesAtAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
}
