
using PreventiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PreventiveMaintenance;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class PreventiveMaintenanceRepository : MongoRepository<PreventiveMaintenanceEntity>,
    IPreventiveMaintenanceRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "preventive_maintenance_schedules";

    public PreventiveMaintenanceRepository(IMongoDatabase database)
        : base(database.GetCollection<PreventiveMaintenanceEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PreventiveMaintenanceEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PreventiveMaintenanceEntity>(
                Builders<PreventiveMaintenanceEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.PmScheduleId),
                new CreateIndexOptions
                {
                    Name = "ix_preventive_maintenance_client_reference"
                }),

            new CreateIndexModel<PreventiveMaintenanceEntity>(
                Builders<PreventiveMaintenanceEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_preventive_maintenance_asset"
                }),

            new CreateIndexModel<PreventiveMaintenanceEntity>(
                Builders<PreventiveMaintenanceEntity>.IndexKeys
                    .Ascending(x => x.NextDueDate),
                new CreateIndexOptions
                {
                    Name = "ix_preventive_maintenance_next_due_date"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
