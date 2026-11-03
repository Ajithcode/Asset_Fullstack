using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AssetModelEntity = A360.MasterDataManagement.Domain.Entities.AssetModel;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AssetModelRepository : MongoRepository<AssetModelEntity>,
    IAssetModelRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_asset_models";

    public AssetModelRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetModelEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetModelEntity>> SearchAsync(
        AssetModelFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetModelEntity>.Filter;
        var filters = new List<FilterDefinition<AssetModelEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ModelName, pattern),
                builder.Regex(x => x.ModelCode, pattern),
                builder.Regex(x => x.Manufacturer!.Name, pattern)));
        }

        AddEqualsIgnoreCase(filters, x => x.Status, filter.Status);
        AddEqualsIgnoreCase(filters, x => x.Lifecycle, filter.Lifecycle);

        if (!string.IsNullOrWhiteSpace(filter.ManufacturerId))
        {
            filters.Add(builder.Eq(x => x.Manufacturer!.Id, filter.ManufacturerId));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssetTypeId))
        {
            filters.Add(builder.Eq(x => x.AssetType!.Id, filter.AssetTypeId));
        }

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
            .SortBy(x => x.ModelName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetModelEntity?> GetByModelCodeAsync(
        string modelCode,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetModelEntity>.Filter;
        var filter = builder.Regex(x => x.ModelCode, ExactIgnoreCase(modelCode))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithAssetTypeAsync(
        string assetTypeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.AssetType!.Id == assetTypeId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Category!.Id == categoryId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithManufacturerAsync(
        string manufacturerId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Manufacturer!.Id == manufacturerId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetModelEntity>> GetByManufacturersAsync(
        IEnumerable<string> manufacturerIds,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<AssetModelEntity>.Filter.In(x => x.Manufacturer!.Id, manufacturerIds))
            .SortBy(x => x.ModelName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetModelEntity>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<AssetModelEntity>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateManufacturerNameAsync(
        string manufacturerId,
        string manufacturerName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Manufacturer!.Id == manufacturerId,
            Builders<AssetModelEntity>.Update.Set(x => x.Manufacturer!.Name, manufacturerName),
            cancellationToken: cancellationToken);
    }

    public async Task SetManufacturerModelsAsync(
        string manufacturerId,
        string manufacturerName,
        IReadOnlyCollection<string> modelIds,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetModelEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.Manufacturer!.Id, manufacturerId) & builder.Nin(x => x.Id, modelIds),
            Builders<AssetModelEntity>.Update.Set(x => x.Manufacturer, null),
            cancellationToken: cancellationToken);

        if (modelIds.Count > 0)
        {
            await Collection.UpdateManyAsync(
                builder.In(x => x.Id, modelIds),
                Builders<AssetModelEntity>.Update.Set(
                    x => x.Manufacturer,
                    new MasterDataReference { Id = manufacturerId, Name = manufacturerName }),
                cancellationToken: cancellationToken);
        }
    }

    public async Task UpdateAssetTypeNameAsync(
        string assetTypeId,
        string assetTypeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.AssetType!.Id == assetTypeId,
            Builders<AssetModelEntity>.Update.Set(x => x.AssetType!.Name, assetTypeName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateCategoryNameAsync(
        string categoryId,
        string categoryName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Category!.Id == categoryId,
            Builders<AssetModelEntity>.Update.Set(x => x.Category!.Name, categoryName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetModelEntity>(
                Builders<AssetModelEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.ModelCode),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_model_client_code"
                }),
            new CreateIndexModel<AssetModelEntity>(
                Builders<AssetModelEntity>.IndexKeys
                    .Ascending(x => x.Manufacturer!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_model_manufacturer"
                }),
            new CreateIndexModel<AssetModelEntity>(
                Builders<AssetModelEntity>.IndexKeys
                    .Ascending(x => x.AssetType!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_model_asset_type"
                }),
            new CreateIndexModel<AssetModelEntity>(
                Builders<AssetModelEntity>.IndexKeys
                    .Ascending(x => x.Category!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_asset_model_category"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static void AddEqualsIgnoreCase(
        List<FilterDefinition<AssetModelEntity>> filters,
        Expression<Func<AssetModelEntity, object?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetModelEntity>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
