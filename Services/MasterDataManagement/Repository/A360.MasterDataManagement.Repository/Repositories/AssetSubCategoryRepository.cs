using System.Text.RegularExpressions;
using AssetSubCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetSubCategory;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AssetSubCategoryRepository : MongoRepository<AssetSubCategoryEntity>,
    IAssetSubCategoryRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_asset_subcategories";

    public AssetSubCategoryRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetSubCategoryEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetSubCategoryEntity>> SearchAsync(
        AssetSubCategoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetSubCategoryEntity>.Filter;
        var filters = new List<FilterDefinition<AssetSubCategoryEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.SubCategoryName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssetType))
        {
            filters.Add(builder.Regex(x => x.AssetType, ExactIgnoreCase(filter.AssetType)));
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryId))
        {
            filters.Add(builder.Eq(x => x.CategoryId, filter.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Asset))
        {
            filters.Add(builder.ElemMatch(x => x.Assets, ReferenceMatches(filter.Asset)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.SubCategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetSubCategoryEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetSubCategoryEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
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
            .Find(x => x.CategoryId == categoryId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateCategoryNameAsync(
        string categoryId,
        string categoryName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.CategoryId == categoryId,
            Builders<AssetSubCategoryEntity>.Update.Set(x => x.CategoryName, categoryName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetSubCategoryEntity>(
                Builders<AssetSubCategoryEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_subcategory_client_code"
                }),
            new CreateIndexModel<AssetSubCategoryEntity>(
                Builders<AssetSubCategoryEntity>.IndexKeys
                    .Ascending(x => x.CategoryId),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_subcategory_category"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static FilterDefinition<MasterDataReference> ReferenceMatches(string value)
    {
        var builder = Builders<MasterDataReference>.Filter;
        return builder.Or(
            builder.Eq(x => x.Id, value),
            builder.Regex(x => x.Name, ExactIgnoreCase(value)));
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
