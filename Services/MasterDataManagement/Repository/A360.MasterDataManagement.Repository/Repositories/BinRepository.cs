using System.Text.RegularExpressions;
using BinEntity = A360.MasterDataManagement.Domain.Entities.Bin;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class BinRepository : MongoRepository<BinEntity>,
    IBinRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_bins";

    public BinRepository(IMongoDatabase database)
        : base(database.GetCollection<BinEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<BinEntity>> SearchAsync(
        BinFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BinEntity>.Filter;
        var filters = new List<FilterDefinition<BinEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.BinName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.StoreId))
        {
            filters.Add(builder.Eq(x => x.Store!.Id, filter.StoreId));
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
            .SortBy(x => x.BinName)
            .ToListAsync(cancellationToken);
    }

    public async Task<BinEntity?> GetByCodeAsync(
        string code,
        string storeId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BinEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.Store!.Id, storeId)
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithStoreAsync(
        string storeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Store!.Id == storeId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateStoreNameAsync(
        string storeId,
        string storeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Store!.Id == storeId,
            Builders<BinEntity>.Update.Set(x => x.Store!.Name, storeName),
            cancellationToken: cancellationToken);
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
            Builders<BinEntity>.Update.Set(x => x.Warehouse!.Name, warehouseName),
            cancellationToken: cancellationToken);
    }

    public async Task SetWarehouseForStoreAsync(
        string storeId,
        MasterDataReference? warehouse,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Store!.Id == storeId,
            Builders<BinEntity>.Update.Set(x => x.Warehouse, warehouse),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<BinEntity>(
                Builders<BinEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Store!.Id)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_bin_client_code"
                }),
            new CreateIndexModel<BinEntity>(
                Builders<BinEntity>.IndexKeys
                    .Ascending(x => x.Store!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_bin_store"
                }),
            new CreateIndexModel<BinEntity>(
                Builders<BinEntity>.IndexKeys
                    .Ascending(x => x.Warehouse!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_bin_warehouse"
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
