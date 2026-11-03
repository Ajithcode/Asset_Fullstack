using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class TrackingTagRepository
    : MongoRepository<TrackingTag>, ITrackingTagRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "TrackingTag";

    // Compares text ignoring case, for tag id uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public TrackingTagRepository(IMongoDatabase database)
        : base(database.GetCollection<TrackingTag>(CollectionName))
    {
    }

    public async Task<TrackingTag?> GetByTagIdAsync(string tagId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(tag => tag.TagId == tagId && !tag.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TagIdExistsAsync(string tagId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(tag => tag.TagId == tagId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TrackingTag>(
                Builders<TrackingTag>.IndexKeys.Ascending(tag => tag.TagId),
                new CreateIndexOptions { Name = "ix_tracking_tag_tag_id", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
