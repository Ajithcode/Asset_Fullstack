using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AssetTypeEntity = A360.MasterDataManagement.Domain.Entities.AssetType;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AssetTypeRepository : MongoRepository<AssetTypeEntity>,
    IAssetTypeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_asset_types";

    public AssetTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetTypeEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetTypeEntity>> SearchAsync(
        AssetTypeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetTypeEntity>.Filter;
        var filters = new List<FilterDefinition<AssetTypeEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.AssetTypeName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        AddEqualsIgnoreCase(filters, x => x.Status, filter.Status);
        AddEqualsIgnoreCase(filters, x => x.AssetClass, filter.AssetClass);
        AddEqualsIgnoreCase(filters, x => x.DepreciationMethod, filter.DepreciationMethod);

        if (!string.IsNullOrWhiteSpace(filter.CategoryId))
        {
            filters.Add(builder.Eq(x => x.Category!.Id, filter.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.AssetTypeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetTypeEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetTypeEntity>.Filter;
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
            Builders<AssetTypeEntity>.Update.Set(x => x.Category!.Name, categoryName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetTypeEntity>(
                Builders<AssetTypeEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_type_client_code"
                }),
            new CreateIndexModel<AssetTypeEntity>(
                Builders<AssetTypeEntity>.IndexKeys
                    .Ascending(x => x.Category!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_type_category"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static void AddEqualsIgnoreCase(
        List<FilterDefinition<AssetTypeEntity>> filters,
        Expression<Func<AssetTypeEntity, object?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetTypeEntity>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
