using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetImageRepository
    : MongoRepository<AssetImage>, IAssetImageRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetImage";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetImageRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetImage>(CollectionName))
    {
    }

    public async Task<AssetImageQueryResult> QueryAsync(AssetImageQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetImage>>
        {
            Builders<AssetImage>.Filter.Eq(image => image.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetImage>.Filter.Or(
                Builders<AssetImage>.Filter.Regex(image => image.ImageName, pattern),
                Builders<AssetImage>.Filter.Regex(image => image.FileName, pattern),
                Builders<AssetImage>.Filter.Regex(image => image.AssetName, pattern),
                Builders<AssetImage>.Filter.Regex(image => image.AssetTag, pattern),
                Builders<AssetImage>.Filter.Regex(image => image.AssetRegistrationId, pattern),
                Builders<AssetImage>.Filter.Regex(image => image.Location, pattern),
                Builders<AssetImage>.Filter.Regex("tags", pattern)));
        }

        AddEquals(filters, image => image.AssetRegistrationId, query.AssetRegistrationId);
        AddEquals(filters, image => image.ImageType, query.ImageType);

        var filter = Builders<AssetImage>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetImage>.Sort.Descending(image => image.CreatedAt).Ascending(image => image.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return new AssetImageQueryResult(items, totalCount);
    }

    private static void AddEquals(
        List<FilterDefinition<AssetImage>> filters,
        System.Linq.Expressions.Expression<Func<AssetImage, string?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetImage>.Filter.Eq(field, value.Trim()));
        }
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetImage>(
                Builders<AssetImage>.IndexKeys
                    .Ascending(image => image.AssetRegistrationId)
                    .Descending(image => image.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_image_asset_registration_id_created_at" }),
            new CreateIndexModel<AssetImage>(
                Builders<AssetImage>.IndexKeys.Descending(image => image.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_image_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
