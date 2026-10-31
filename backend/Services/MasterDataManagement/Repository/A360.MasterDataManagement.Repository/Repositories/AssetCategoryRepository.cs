using System.Text.RegularExpressions;
using AssetCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetCategory;
using A360.MasterDataManagement.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AssetCategoryRepository : MongoRepository<AssetCategoryEntity>,
    IAssetCategoryRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_asset_categories";

    public AssetCategoryRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetCategoryEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetCategoryEntity>> SearchAsync(
        AssetCategoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetCategoryEntity>.Filter;
        var filters = new List<FilterDefinition<AssetCategoryEntity>>();

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

        if (!string.IsNullOrWhiteSpace(filter.AssetType))
        {
            filters.Add(builder.Regex(x => x.AssetType, ExactIgnoreCase(filter.AssetType)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentCategoryId))
        {
            filters.Add(builder.Eq(x => x.ParentCategory!.Id, filter.ParentCategoryId));
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
            .SortBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetCategoryEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetCategoryEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> FindSubcategoryCodeInUseAsync(
        IReadOnlyCollection<string> codes,
        string? clientId,
        string? exceptCategoryId,
        CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
        {
            return null;
        }

        var builder = Builders<AssetCategoryEntity>.Filter;
        var codeFilter = Builders<AssetSubcategory>.Filter.Or(
            codes.Select(code => Builders<AssetSubcategory>.Filter.Regex(x => x.Code, ExactIgnoreCase(code))));
        var filter = builder.Eq(x => x.ClientId, clientId)
            & builder.ElemMatch(x => x.Subcategories, codeFilter);

        if (!string.IsNullOrEmpty(exceptCategoryId))
        {
            filter &= builder.Ne(x => x.Id, exceptCategoryId);
        }

        var owner = await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return owner?.Subcategories
            .Select(x => x.Code)
            .FirstOrDefault(x => codes.Contains(x, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<AssetCategoryEntity>> GetChildrenAsync(
        string parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentCategory!.Id == parentCategoryId)
            .SortBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        string parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentCategory!.Id == parentCategoryId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateParentCategoryNameAsync(
        string parentCategoryId,
        string parentCategoryName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.ParentCategory!.Id == parentCategoryId,
            Builders<AssetCategoryEntity>.Update.Set(x => x.ParentCategory!.Name, parentCategoryName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetCategoryEntity>(
                Builders<AssetCategoryEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_category_client_code"
                }),
            new CreateIndexModel<AssetCategoryEntity>(
                Builders<AssetCategoryEntity>.IndexKeys
                    .Ascending(x => x.ParentCategory!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_category_parent"
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
