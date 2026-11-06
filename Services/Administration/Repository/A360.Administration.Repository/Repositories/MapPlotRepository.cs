using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapPlotRepository : MongoRepository<MapPlot>, IMapPlotRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_plots";

    public MapPlotRepository(IMongoDatabase database)
        : base(database.GetCollection<MapPlot>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MapPlot>> GetByFloorPlanAsync(
        string floorPlanId,
        string? kind,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<MapPlot>.Filter;
        var filter = builder.Eq(plot => plot.FloorPlanId, floorPlanId) & builder.Eq(plot => plot.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(kind))
        {
            filter &= builder.Eq(plot => plot.Kind, kind);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(plot => plot.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(plot => plot.TenantId, tenantId);
        }

        return await Collection
            .Find(filter)
            .SortBy(plot => plot.Kind)
            .ThenBy(plot => plot.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<MapPlot?> GetByKeyAsync(string floorPlanId, string kind, string itemId, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        var builder = Builders<MapPlot>.Filter;
        var filter = builder.Eq(plot => plot.FloorPlanId, floorPlanId)
            & builder.Eq(plot => plot.Kind, kind)
            & builder.Eq(plot => plot.ItemId, itemId);
        if (!includeDeleted)
        {
            filter &= builder.Eq(plot => plot.IsDeleted, false);
        }

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> CountByFloorPlansAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default)
    {
        if (floorPlanIds.Count == 0)
        {
            return [];
        }

        var planIds = await Collection
            .Find(ActiveOn(floorPlanIds))
            .Project(plot => plot.FloorPlanId)
            .ToListAsync(cancellationToken);
        return planIds.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task<Dictionary<string, int>> CountByKindAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default)
    {
        if (floorPlanIds.Count == 0)
        {
            return [];
        }

        var kinds = await Collection
            .Find(ActiveOn(floorPlanIds))
            .Project(plot => plot.Kind)
            .ToListAsync(cancellationToken);
        return kinds.GroupBy(kind => kind).ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task ClearZoneAsync(string zoneId, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MapPlot>.Filter.Eq(plot => plot.ZoneId, zoneId);
        var update = Builders<MapPlot>.Update
            .Set(plot => plot.ZoneId, null)
            .Set(plot => plot.UpdatedBy, updatedBy)
            .Set(plot => plot.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MapPlot>.Filter.Eq(plot => plot.FloorPlanId, floorPlanId)
            & Builders<MapPlot>.Filter.Eq(plot => plot.IsDeleted, false);
        var update = Builders<MapPlot>.Update
            .Set(plot => plot.IsDeleted, true)
            .Set(plot => plot.UpdatedBy, updatedBy)
            .Set(plot => plot.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            // One plot per item per plan; removing an item soft-deletes the row and placing it again revives it.
            new CreateIndexModel<MapPlot>(
                Builders<MapPlot>.IndexKeys
                    .Ascending(plot => plot.FloorPlanId)
                    .Ascending(plot => plot.Kind)
                    .Ascending(plot => plot.ItemId),
                new CreateIndexOptions { Name = "ux_maps_plots_floor_plan_id_kind_item_id", Unique = true }),
            new CreateIndexModel<MapPlot>(
                Builders<MapPlot>.IndexKeys.Ascending(plot => plot.ZoneId),
                new CreateIndexOptions { Name = "ix_maps_plots_zone_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static FilterDefinition<MapPlot> ActiveOn(IReadOnlyCollection<string> floorPlanIds)
    {
        var builder = Builders<MapPlot>.Filter;
        return builder.In(plot => plot.FloorPlanId, floorPlanIds) & builder.Eq(plot => plot.IsDeleted, false);
    }
}
