using System.Text.RegularExpressions;
using WarehouseEntity = A360.MasterDataManagement.Domain.Entities.Warehouse;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class WarehouseRepository : MongoRepository<WarehouseEntity>,
    IWarehouseRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_warehouses";

    public WarehouseRepository(IMongoDatabase database)
        : base(database.GetCollection<WarehouseEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WarehouseEntity>> SearchAsync(
        WarehouseFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<WarehouseEntity>.Filter;
        var filters = new List<FilterDefinition<WarehouseEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.WarehouseName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ManagerId))
        {
            filters.Add(builder.Eq(x => x.Manager!.Id, filter.ManagerId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.WarehouseName)
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<WarehouseEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithManagerAsync(
        string managerId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Manager!.Id == managerId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateManagerNameAsync(
        string managerId,
        string managerName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Manager!.Id == managerId,
            Builders<WarehouseEntity>.Update.Set(x => x.Manager!.Name, managerName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WarehouseEntity>(
                Builders<WarehouseEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_warehouse_client_code"
                }),
            new CreateIndexModel<WarehouseEntity>(
                Builders<WarehouseEntity>.IndexKeys
                    .Ascending(x => x.Manager!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_warehouse_manager"
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
