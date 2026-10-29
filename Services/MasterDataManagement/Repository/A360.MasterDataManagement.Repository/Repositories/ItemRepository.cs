using System.Text.RegularExpressions;
using ItemEntity = A360.MasterDataManagement.Domain.Entities.Item;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ItemRepository : MongoRepository<ItemEntity>,
    IItemRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_items";

    public ItemRepository(IMongoDatabase database)
        : base(database.GetCollection<ItemEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ItemEntity>> SearchAsync(
        ItemFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ItemEntity>.Filter;
        var filters = new List<FilterDefinition<ItemEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ItemName, pattern),
                builder.Regex(x => x.ItemCode, pattern),
                builder.Regex(x => x.PartNumber, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryId))
        {
            filters.Add(builder.Eq(x => x.Category!.Id, filter.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.UomId))
        {
            filters.Add(builder.Eq(x => x.Uom!.Id, filter.UomId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.ItemName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ItemEntity?> GetByItemCodeAsync(
        string itemCode,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ItemEntity>.Filter;
        var filter = builder.Regex(x => x.ItemCode, ExactIgnoreCase(itemCode))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Category!.Id == categoryId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateCategoryNameAsync(
        string categoryId,
        string categoryName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Category!.Id == categoryId,
            Builders<ItemEntity>.Update.Set(x => x.Category!.Name, categoryName),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> AnyWithUomAsync(
        string uomId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Uom!.Id == uomId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateUomNameAsync(
        string uomId,
        string uomName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Uom!.Id == uomId,
            Builders<ItemEntity>.Update.Set(x => x.Uom!.Name, uomName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ItemEntity>(
                Builders<ItemEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.ItemCode),
                new CreateIndexOptions
                {
                    Name = "ix_master_item_client_item_code"
                }),
            new CreateIndexModel<ItemEntity>(
                Builders<ItemEntity>.IndexKeys
                    .Ascending(x => x.Category!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_item_category"
                }),
            new CreateIndexModel<ItemEntity>(
                Builders<ItemEntity>.IndexKeys
                    .Ascending(x => x.Uom!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_item_uom"
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
