using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ITrackingTagRepository : IMongoRepository<TrackingTag>
{
    Task<TrackingTag?> GetByTagIdAsync(string tagId, CancellationToken cancellationToken = default);

    /// <summary>Tag ids are unique across all technologies, ignoring case.</summary>
    Task<bool> TagIdExistsAsync(string tagId, CancellationToken cancellationToken = default);

    /// <summary>Every tag that is not deleted.</summary>
    Task<IReadOnlyList<TrackingTag>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The tag id of another tag of the same technology carrying this value (MAC, EPC, encoded value, UID), or null.</summary>
    Task<string?> FindTagIdByValueAsync(string technology, string value, string? exceptTagId, CancellationToken cancellationToken = default);

    /// <summary>The highest numeric tag number used with this prefix (e.g. 2951 for BLE-002951), or 0.</summary>
    Task<long> GetHighestTagNumberAsync(string prefix, CancellationToken cancellationToken = default);
}
