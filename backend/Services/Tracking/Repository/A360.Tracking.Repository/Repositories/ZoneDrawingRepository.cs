using MongoDB.Driver;
using A360.Repository.Repositories;
using ZoneDrawingEntity = A360.Tracking.Domain.Entities.ZoneDrawing;

namespace A360.Tracking.Repository.Repositories;

public sealed class ZoneDrawingRepository : MongoRepository<ZoneDrawingEntity>, IZoneDrawingRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "zone_drawings";

    public ZoneDrawingRepository(IMongoDatabase database)
        : base(database.GetCollection<ZoneDrawingEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ZoneDrawingEntity>> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default)
    {
        if (!MongoObjectId.IsValid(zoneId))
        {
            return [];
        }

        return await Collection
            .Find(drawing => drawing.ZoneId == zoneId && !drawing.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ZoneDrawingEntity>(
                Builders<ZoneDrawingEntity>.IndexKeys.Ascending(drawing => drawing.ProjectId),
                new CreateIndexOptions { Name = "ix_zone_drawings_project_id" }),
            new CreateIndexModel<ZoneDrawingEntity>(
                Builders<ZoneDrawingEntity>.IndexKeys.Ascending(drawing => drawing.FloorId),
                new CreateIndexOptions { Name = "ix_zone_drawings_floor_id" }),
            new CreateIndexModel<ZoneDrawingEntity>(
                Builders<ZoneDrawingEntity>.IndexKeys.Ascending(drawing => drawing.ZoneId),
                new CreateIndexOptions { Name = "ix_zone_drawings_zone_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
