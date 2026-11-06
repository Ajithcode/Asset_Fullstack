using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapLayerSetRepository : MongoRepository<MapLayerSet>, IMapLayerSetRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_layer_sets";

    public MapLayerSetRepository(IMongoDatabase database)
        : base(database.GetCollection<MapLayerSet>(CollectionName))
    {
    }

    public async Task<MapLayerSet?> GetByLayerSetIdAsync(string layerSetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(layerSetId))
        {
            return null;
        }

        return await Collection
            .Find(set => set.LayerSetId == layerSetId && !set.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MapLayerSet>> GetAsync(string? clientId, string? tenantId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Scope(clientId, tenantId))
            .SortBy(set => set.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, string? clientId, string? tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Scope(clientId, tenantId)
            & Builders<MapLayerSet>.Filter.Regex(set => set.Name, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i"));

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapLayerSet>(
                Builders<MapLayerSet>.IndexKeys.Ascending(set => set.LayerSetId),
                new CreateIndexOptions { Name = "ux_maps_layer_sets_layer_set_id", Unique = true }),
            new CreateIndexModel<MapLayerSet>(
                Builders<MapLayerSet>.IndexKeys
                    .Ascending(set => set.TenantId)
                    .Ascending(set => set.ClientId),
                new CreateIndexOptions { Name = "ix_maps_layer_sets_tenant_id_client_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static FilterDefinition<MapLayerSet> Scope(string? clientId, string? tenantId)
    {
        var builder = Builders<MapLayerSet>.Filter;
        var filter = builder.Eq(set => set.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(set => set.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(set => set.TenantId, tenantId);
        }

        return filter;
    }
}
