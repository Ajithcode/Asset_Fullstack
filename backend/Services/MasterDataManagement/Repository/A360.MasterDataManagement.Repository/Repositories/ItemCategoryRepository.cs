using System.Text.RegularExpressions;
using ItemCategoryEntity = A360.MasterDataManagement.Domain.Entities.ItemCategory;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ItemCategoryRepository : MongoRepository<ItemCategoryEntity>,
    IItemCategoryRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_item_categories";

    public ItemCategoryRepository(IMongoDatabase database)
        : base(database.GetCollection<ItemCategoryEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ItemCategoryEntity>> SearchAsync(
        ItemCategoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ItemCategoryEntity>.Filter;
        var filters = new List<FilterDefinition<ItemCategoryEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.CategoryName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentCategoryId))
        {
            filters.Add(builder.Eq(x => x.ParentCategory!.Id, filter.ParentCategoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ItemCategoryEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ItemCategoryEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ItemCategoryEntity>> GetChildrenAsync(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentCategory!.Id == parentId)
            .SortBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentCategory!.Id == parentId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateParentCategoryNameAsync(
        string parentId,
        string parentName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.ParentCategory!.Id == parentId,
            Builders<ItemCategoryEntity>.Update.Set(x => x.ParentCategory!.Name, parentName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ItemCategoryEntity>(
                Builders<ItemCategoryEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_item_category_client_code"
                }),
            new CreateIndexModel<ItemCategoryEntity>(
                Builders<ItemCategoryEntity>.IndexKeys
                    .Ascending(x => x.ParentCategory!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_item_category_parent_category"
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
