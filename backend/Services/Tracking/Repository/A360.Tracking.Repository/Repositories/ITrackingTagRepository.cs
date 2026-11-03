using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface ITrackingTagRepository : IMongoRepository<TrackingTag>
{
    Task<TrackingTag?> GetByTagIdAsync(string tagId, CancellationToken cancellationToken = default);

    /// <summary>Tag ids are unique across all technologies, ignoring case.</summary>
    Task<bool> TagIdExistsAsync(string tagId, CancellationToken cancellationToken = default);
}
