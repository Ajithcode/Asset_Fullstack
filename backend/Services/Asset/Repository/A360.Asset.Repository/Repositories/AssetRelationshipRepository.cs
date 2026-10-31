using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetRelationshipRepository
    : MongoRepository<AssetRelationship>, IAssetRelationshipRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetRelationship";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetRelationshipRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetRelationship>(CollectionName))
    {
    }

    public async Task<AssetRelationshipQueryResult> QueryAsync(AssetRelationshipQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetRelationship>>
        {
            Builders<AssetRelationship>.Filter.Eq(relationship => relationship.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetRelationship>.Filter.Or(
                Builders<AssetRelationship>.Filter.Regex(relationship => relationship.MainAssetRegistrationId, pattern),
                Builders<AssetRelationship>.Filter.Regex(relationship => relationship.MainAssetName, pattern),
                Builders<AssetRelationship>.Filter.Regex(relationship => relationship.MainAssetTag, pattern),
                Builders<AssetRelationship>.Filter.Regex("sub_assets.name", pattern),
                Builders<AssetRelationship>.Filter.Regex("sub_assets.tag", pattern),
                Builders<AssetRelationship>.Filter.Regex("sub_assets.asset_registration_id", pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            var assetRegistrationId = query.AssetRegistrationId.Trim();
            filters.Add(Builders<AssetRelationship>.Filter.Or(
                Builders<AssetRelationship>.Filter.Eq(relationship => relationship.MainAssetRegistrationId, assetRegistrationId),
                Builders<AssetRelationship>.Filter.Eq("sub_assets.asset_registration_id", assetRegistrationId)));
        }

        if (!string.IsNullOrWhiteSpace(query.RelationshipType))
        {
            filters.Add(Builders<AssetRelationship>.Filter.Eq("sub_assets.relationship_type", query.RelationshipType.Trim()));
        }

        var filter = Builders<AssetRelationship>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetRelationship>.Sort.Descending(relationship => relationship.UpdatedAt).Descending(relationship => relationship.CreatedAt).Ascending(relationship => relationship.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var linkCounts = await Collection
            .Find(filter)
            .Project(relationship => relationship.SubAssets.Count)
            .ToListAsync(cancellationToken);

        return new AssetRelationshipQueryResult(items, totalCount, linkCounts.Sum());
    }

    public async Task<AssetRelationship?> GetByMainAssetAsync(string mainAssetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(relationship => relationship.MainAssetRegistrationId == mainAssetRegistrationId && !relationship.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetRelationship>(
                Builders<AssetRelationship>.IndexKeys.Ascending(relationship => relationship.MainAssetRegistrationId),
                new CreateIndexOptions { Name = "ix_asset_relationship_main_asset_registration_id" }),
            new CreateIndexModel<AssetRelationship>(
                Builders<AssetRelationship>.IndexKeys.Ascending("sub_assets.asset_registration_id"),
                new CreateIndexOptions { Name = "ix_asset_relationship_sub_asset_registration_id" }),
            new CreateIndexModel<AssetRelationship>(
                Builders<AssetRelationship>.IndexKeys.Descending(relationship => relationship.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_relationship_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
