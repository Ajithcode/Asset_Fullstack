using System.Text.RegularExpressions;
using A360.MasterDataManagement.Domain.Entities;
using A360.Repository.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AssetSubTypeRepository : MongoRepository<AssetSubType>,
    IAssetSubTypeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_asset_sub_types";

    public AssetSubTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetSubType>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetSubType>> SearchAsync(
        AssetSubTypeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetSubType>.Filter;
        var filters = new List<FilterDefinition<AssetSubType>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.SubTypeName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssetTypeId))
        {
            filters.Add(builder.Eq(x => x.AssetType!.Id, filter.AssetTypeId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.SubTypeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetSubType?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetSubType>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithAssetTypeAsync(
        string assetTypeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.AssetType!.Id == assetTypeId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateAssetTypeNameAsync(
        string assetTypeId,
        string assetTypeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.AssetType!.Id == assetTypeId,
            Builders<AssetSubType>.Update.Set(x => x.AssetType!.Name, assetTypeName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetSubType>(
                Builders<AssetSubType>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions { Name = "ix_master_asset_sub_type_client_code" }),
            new CreateIndexModel<AssetSubType>(
                Builders<AssetSubType>.IndexKeys.Ascending(x => x.AssetType!.Id),
                new CreateIndexOptions { Name = "ix_master_asset_sub_type_asset_type" }),
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
