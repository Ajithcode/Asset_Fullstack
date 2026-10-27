using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class GeofenceRepository
    : MongoRepository<Geofence>, IGeofenceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "Geofence";

    // Compares text ignoring case, for geofence name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public GeofenceRepository(IMongoDatabase database)
        : base(database.GetCollection<Geofence>(CollectionName))
    {
    }

    public async Task<bool> NameExistsAsync(string siteName, string name, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(geofence => geofence.SiteName == siteName && geofence.Name == name, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Geofence>(
                Builders<Geofence>.IndexKeys
                    .Ascending(geofence => geofence.SiteName)
                    .Ascending(geofence => geofence.Name),
                new CreateIndexOptions { Name = "ix_geofence_site_name_name", Unique = true, Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
