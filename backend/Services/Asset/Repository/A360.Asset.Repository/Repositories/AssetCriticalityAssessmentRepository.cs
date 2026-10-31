using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetCriticalityAssessmentRepository
    : MongoRepository<AssetCriticalityAssessment>, IAssetCriticalityAssessmentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCriticalityAssessment";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetCriticalityAssessmentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetCriticalityAssessment>(CollectionName))
    {
    }

    public async Task<AssetCriticalityQueryResult> QueryCurrentAsync(AssetCriticalityQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetCriticalityAssessment>>
        {
            Builders<AssetCriticalityAssessment>.Filter.Eq(assessment => assessment.IsDeleted, false),
            Builders<AssetCriticalityAssessment>.Filter.Eq(assessment => assessment.IsCurrent, true)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetCriticalityAssessment>.Filter.Or(
                Builders<AssetCriticalityAssessment>.Filter.Regex(assessment => assessment.AssetRegistrationId, pattern),
                Builders<AssetCriticalityAssessment>.Filter.Regex(assessment => assessment.AssetName, pattern),
                Builders<AssetCriticalityAssessment>.Filter.Regex(assessment => assessment.AssetTag, pattern)));
        }

        AddEquals(filters, assessment => assessment.Category, query.Category);
        AddEquals(filters, assessment => assessment.Location, query.Location);
        AddEquals(filters, assessment => assessment.CriticalityLevel, query.CriticalityLevel);

        var filter = Builders<AssetCriticalityAssessment>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetCriticalityAssessment>.Sort.Ascending(assessment => assessment.AssetRegistrationId))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return new AssetCriticalityQueryResult(items, totalCount);
    }

    public async Task<AssetCriticalityAssessment?> GetCurrentAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(assessment => assessment.AssetRegistrationId == assetRegistrationId && assessment.IsCurrent && !assessment.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetCriticalityAssessment>> GetHistoryAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(assessment => assessment.AssetRegistrationId == assetRegistrationId && !assessment.IsDeleted)
            .Sort(Builders<AssetCriticalityAssessment>.Sort
                .Descending(assessment => assessment.AssessmentDate)
                .Descending(assessment => assessment.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task ClearCurrentAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            assessment => assessment.AssetRegistrationId == assetRegistrationId && assessment.IsCurrent,
            Builders<AssetCriticalityAssessment>.Update.Set(assessment => assessment.IsCurrent, false),
            cancellationToken: cancellationToken);
    }

    private static void AddEquals(
        List<FilterDefinition<AssetCriticalityAssessment>> filters,
        System.Linq.Expressions.Expression<Func<AssetCriticalityAssessment, string?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetCriticalityAssessment>.Filter.Eq(field, value.Trim()));
        }
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetCriticalityAssessment>(
                Builders<AssetCriticalityAssessment>.IndexKeys
                    .Ascending(assessment => assessment.AssetRegistrationId)
                    .Descending(assessment => assessment.AssessmentDate),
                new CreateIndexOptions { Name = "ix_asset_criticality_asset_registration_id_assessment_date" }),
            new CreateIndexModel<AssetCriticalityAssessment>(
                Builders<AssetCriticalityAssessment>.IndexKeys
                    .Ascending(assessment => assessment.IsCurrent)
                    .Ascending(assessment => assessment.AssetRegistrationId),
                new CreateIndexOptions { Name = "ix_asset_criticality_is_current_asset_registration_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
