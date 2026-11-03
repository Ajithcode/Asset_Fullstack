using MongoDB.Driver;
using A360.Repository.Repositories;
using DeviceDrawingEntity = A360.Tracking.Domain.Entities.DeviceDrawing;

namespace A360.Tracking.Repository.Repositories;

public sealed class DeviceDrawingRepository : MongoRepository<DeviceDrawingEntity>, IDeviceDrawingRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "device_drawings";

    public DeviceDrawingRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceDrawingEntity>(CollectionName))
    {
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DeviceDrawingEntity>(
                Builders<DeviceDrawingEntity>.IndexKeys.Ascending(drawing => drawing.ProjectId),
                new CreateIndexOptions { Name = "ix_device_drawings_project_id" }),
            new CreateIndexModel<DeviceDrawingEntity>(
                Builders<DeviceDrawingEntity>.IndexKeys.Ascending(drawing => drawing.FloorId),
                new CreateIndexOptions { Name = "ix_device_drawings_floor_id" }),
            new CreateIndexModel<DeviceDrawingEntity>(
                Builders<DeviceDrawingEntity>.IndexKeys.Ascending(drawing => drawing.ZoneId),
                new CreateIndexOptions { Name = "ix_device_drawings_zone_id" }),
            new CreateIndexModel<DeviceDrawingEntity>(
                Builders<DeviceDrawingEntity>.IndexKeys.Ascending(drawing => drawing.DeviceReferenceId),
                new CreateIndexOptions { Name = "ix_device_drawings_device_reference_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
