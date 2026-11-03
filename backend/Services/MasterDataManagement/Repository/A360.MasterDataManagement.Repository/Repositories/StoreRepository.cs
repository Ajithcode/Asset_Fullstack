using System.Text.RegularExpressions;
using StoreEntity = A360.MasterDataManagement.Domain.Entities.Store;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class StoreRepository : MongoRepository<StoreEntity>,
    IStoreRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_stores";

    public StoreRepository(IMongoDatabase database)
        : base(database.GetCollection<StoreEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<StoreEntity>> SearchAsync(
        StoreFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<StoreEntity>.Filter;
        var filters = new List<FilterDefinition<StoreEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.StoreName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.WarehouseId))
        {
            filters.Add(builder.Eq(x => x.Warehouse!.Id, filter.WarehouseId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.StoreName)
            .ToListAsync(cancellationToken);
    }

    public async Task<StoreEntity?> GetByCodeAsync(
        string code,
        string warehouseId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<StoreEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.Warehouse!.Id, warehouseId)
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithWarehouseAsync(
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Warehouse!.Id == warehouseId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateWarehouseNameAsync(
        string warehouseId,
        string warehouseName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Warehouse!.Id == warehouseId,
            Builders<StoreEntity>.Update.Set(x => x.Warehouse!.Name, warehouseName),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByWarehouseAsync(
        IEnumerable<string> warehouseIds,
        CancellationToken cancellationToken = default)
    {
        var counts = await Collection
            .Aggregate()
            .Match(Builders<StoreEntity>.Filter.In(x => x.Warehouse!.Id, warehouseIds))
            .Group(x => x.Warehouse!.Id, g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.Id!, x => x.Count);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<StoreEntity>(
                Builders<StoreEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Warehouse!.Id)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_store_client_code"
                }),
            new CreateIndexModel<StoreEntity>(
                Builders<StoreEntity>.IndexKeys
                    .Ascending(x => x.Warehouse!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_store_warehouse"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
