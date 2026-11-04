using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetMergeRepository
    : MongoRepository<AssetMerge>, IAssetMergeRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetMerge";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetMergeRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetMerge>(CollectionName))
    {
    }

    public async Task<AssetMergeQueryResult> QueryAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetMerge>>
        {
            Builders<AssetMerge>.Filter.Eq(merge => merge.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
            filters.Add(Builders<AssetMerge>.Filter.Or(
                Builders<AssetMerge>.Filter.Regex(merge => merge.PrimaryTag, pattern),
                Builders<AssetMerge>.Filter.Regex("merged_tags", pattern)));
        }

        var filter = Builders<AssetMerge>.Filter.And(filters);

        var items = await Collection
            .Find(filter)
            .SortByDescending(merge => merge.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return new AssetMergeQueryResult(items, totalCount);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetMerge>(
                Builders<AssetMerge>.IndexKeys.Ascending(merge => merge.PrimaryTag),
                new CreateIndexOptions { Name = "ix_asset_merge_primary_tag" }),
            new CreateIndexModel<AssetMerge>(
                Builders<AssetMerge>.IndexKeys.Descending(merge => merge.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_merge_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
