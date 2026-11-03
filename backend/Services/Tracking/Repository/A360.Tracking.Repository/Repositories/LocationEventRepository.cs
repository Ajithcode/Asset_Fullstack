using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class LocationEventRepository
    : MongoRepository<LocationEvent>, ILocationEventRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "LocationEvent";

    // Compares text ignoring case, for event id lookups.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public LocationEventRepository(IMongoDatabase database)
        : base(database.GetCollection<LocationEvent>(CollectionName))
    {
    }

    public async Task<LocationEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(locationEvent => locationEvent.EventId == eventId && !locationEvent.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<LocationEvent>(
                Builders<LocationEvent>.IndexKeys.Ascending(locationEvent => locationEvent.EventId),
                new CreateIndexOptions { Name = "ix_location_event_event_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<LocationEvent>(
                Builders<LocationEvent>.IndexKeys.Descending(locationEvent => locationEvent.EventTime),
                new CreateIndexOptions { Name = "ix_location_event_event_time" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
