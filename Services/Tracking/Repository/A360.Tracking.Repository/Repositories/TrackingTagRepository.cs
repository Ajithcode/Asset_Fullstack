using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class TrackingTagRepository
    : MongoRepository<TrackingTag>, ITrackingTagRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "TrackingTag";

    // Compares text ignoring case, for tag id and value uniqueness.
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

    public async Task<IReadOnlyList<TrackingTag>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(tag => !tag.IsDeleted)
            .SortByDescending(tag => tag.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> FindTagIdByValueAsync(
        string technology,
        string value,
        string? exceptTagId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TrackingTag>.Filter;
        var match = filter.Eq(tag => tag.Technology, technology)
            & filter.Eq(tag => tag.Value, value)
            & filter.Eq(tag => tag.IsDeleted, false);

        if (exceptTagId is not null)
        {
            match &= filter.Ne(tag => tag.TagId, exceptTagId);
        }

        return await Collection
            .Find(match, new FindOptions { Collation = CaseInsensitive })
            .Project(tag => tag.TagId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> GetHighestTagNumberAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var tagIds = await Collection
            .Find(Builders<TrackingTag>.Filter.Regex(tag => tag.TagId,
                new BsonRegularExpression("^" + Regex.Escape(prefix) + "-[0-9]+$", "i")))
            .Project(tag => tag.TagId)
            .ToListAsync(cancellationToken);

        return tagIds
            .Select(tagId => long.TryParse(tagId[(prefix.Length + 1)..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TrackingTag>(
                Builders<TrackingTag>.IndexKeys.Ascending(tag => tag.TagId),
                new CreateIndexOptions { Name = "ix_tracking_tag_tag_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<TrackingTag>(
                Builders<TrackingTag>.IndexKeys.Ascending(tag => tag.Technology).Ascending(tag => tag.Value),
                new CreateIndexOptions { Name = "ix_tracking_tag_technology_value", Collation = CaseInsensitive }),
            new CreateIndexModel<TrackingTag>(
                Builders<TrackingTag>.IndexKeys.Ascending(tag => tag.Status),
                new CreateIndexOptions { Name = "ix_tracking_tag_status" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
