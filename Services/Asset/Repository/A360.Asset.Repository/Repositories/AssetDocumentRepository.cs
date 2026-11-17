using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetDocumentRepository
    : MongoRepository<AssetDocumentRecord>, IAssetDocumentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetDocument";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetDocumentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetDocumentRecord>(CollectionName))
    {
    }

    public async Task<AssetDocumentQueryResult> QueryAsync(AssetDocumentQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetDocumentRecord>>
        {
            Builders<AssetDocumentRecord>.Filter.Eq(document => document.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetDocumentRecord>.Filter.Or(
                Builders<AssetDocumentRecord>.Filter.Regex(document => document.DocumentName, pattern),
                Builders<AssetDocumentRecord>.Filter.Regex(document => document.FileName, pattern),
                Builders<AssetDocumentRecord>.Filter.Regex(document => document.AssetName, pattern),
                Builders<AssetDocumentRecord>.Filter.Regex(document => document.AssetTag, pattern),
                Builders<AssetDocumentRecord>.Filter.Regex(document => document.AssetRegistrationId, pattern),
                Builders<AssetDocumentRecord>.Filter.Regex("tags", pattern)));
        }

        AddEquals(filters, document => document.AssetRegistrationId, query.AssetRegistrationId);
        AddEquals(filters, document => document.DocumentType, query.DocumentType);
        AddEquals(filters, document => document.LifecycleStage, query.LifecycleStage);
        // Expired is not stored: it is an active document whose expiry date has passed.
        var today = (query.Today ?? DateTime.UtcNow).Date;
        var status = query.Status?.Trim();
        var document = Builders<AssetDocumentRecord>.Filter;
        var notInactive = document.Ne(item => item.Status, AssetDocumentEffectiveStatus.Inactive);
        var expired = document.Lt(item => item.ExpiryDate, today);
        if (string.Equals(status, AssetDocumentEffectiveStatus.Expired, StringComparison.OrdinalIgnoreCase))
        {
            filters.Add(notInactive & expired);
        }
        else if (string.Equals(status, AssetDocumentEffectiveStatus.Active, StringComparison.OrdinalIgnoreCase))
        {
            filters.Add(notInactive & document.Not(expired));
        }
        else
        {
            AddEquals(filters, item => item.Status, status);
        }

        var filter = Builders<AssetDocumentRecord>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetDocumentRecord>.Sort.Descending(document => document.CreatedAt).Ascending(document => document.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return new AssetDocumentQueryResult(items, totalCount);
    }

    public async Task<IReadOnlyCollection<AssetDocumentRecord>> GetFilterFieldsAsync(string? assetRegistrationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AssetDocumentRecord>.Filter.Eq(document => document.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(assetRegistrationId))
        {
            filter &= Builders<AssetDocumentRecord>.Filter.Eq(document => document.AssetRegistrationId, assetRegistrationId.Trim());
        }

        return await Collection
            .Find(filter)
            .Project<AssetDocumentRecord>(Builders<AssetDocumentRecord>.Projection
                .Include(document => document.DocumentType)
                .Include(document => document.LifecycleStage)
                .Include(document => document.Tags)
                .Include(document => document.CreatedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetDocumentRecord>> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AssetDocumentRecord>.Filter.And(
            Builders<AssetDocumentRecord>.Filter.Eq(document => document.AssetRegistrationId, assetRegistrationId.Trim()),
            Builders<AssetDocumentRecord>.Filter.Eq(document => document.IsDeleted, false));

        return await Collection
            .Find(filter)
            .Sort(Builders<AssetDocumentRecord>.Sort.Descending(document => document.CreatedAt).Ascending(document => document.Id))
            .ToListAsync(cancellationToken);
    }

    private static void AddEquals(
        List<FilterDefinition<AssetDocumentRecord>> filters,
        System.Linq.Expressions.Expression<Func<AssetDocumentRecord, string?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetDocumentRecord>.Filter.Eq(field, value.Trim()));
        }
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetDocumentRecord>(
                Builders<AssetDocumentRecord>.IndexKeys
                    .Ascending(document => document.AssetRegistrationId)
                    .Descending(document => document.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_document_asset_registration_id_created_at" }),
            new CreateIndexModel<AssetDocumentRecord>(
                Builders<AssetDocumentRecord>.IndexKeys.Descending(document => document.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_document_created_at" }),
            new CreateIndexModel<AssetDocumentRecord>(
                Builders<AssetDocumentRecord>.IndexKeys.Ascending(document => document.ExpiryDate),
                new CreateIndexOptions { Name = "ix_asset_document_expiry_date" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
