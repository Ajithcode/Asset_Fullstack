using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetConditionAssessmentRepository
    : MongoRepository<AssetConditionAssessment>, IAssetConditionAssessmentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetConditionAssessment";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetConditionAssessmentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetConditionAssessment>(CollectionName))
    {
    }

    public async Task<AssetConditionAssessmentQueryResult> QueryAsync(AssetConditionAssessmentQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetConditionAssessment>>
        {
            Builders<AssetConditionAssessment>.Filter.Eq(assessment => assessment.IsDeleted, false)
        };

        AddEquals(filters, assessment => assessment.AssetRegistrationId, query.AssetRegistrationId);
        AddEquals(filters, assessment => assessment.OverallCondition, query.OverallCondition);

        if (query.FromDate is not null)
        {
            filters.Add(Builders<AssetConditionAssessment>.Filter.Gte(assessment => assessment.AssessmentDate, query.FromDate.Value));
        }

        if (query.ToDate is not null)
        {
            filters.Add(Builders<AssetConditionAssessment>.Filter.Lte(assessment => assessment.AssessmentDate, query.ToDate.Value));
        }

        var filter = Builders<AssetConditionAssessment>.Filter.And(filters);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(LatestFirst)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return new AssetConditionAssessmentQueryResult(items, totalCount);
    }

    public async Task<AssetConditionAssessment?> GetLatestAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(assessment => assessment.AssetRegistrationId == assetRegistrationId && !assessment.IsDeleted)
            .Sort(LatestFirst)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SortDefinition<AssetConditionAssessment> LatestFirst =>
        Builders<AssetConditionAssessment>.Sort
            .Descending(assessment => assessment.AssessmentDate)
            .Descending(assessment => assessment.CreatedAt)
            .Ascending(assessment => assessment.Id);

    private static void AddEquals(
        List<FilterDefinition<AssetConditionAssessment>> filters,
        System.Linq.Expressions.Expression<Func<AssetConditionAssessment, string?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetConditionAssessment>.Filter.Eq(field, value.Trim()));
        }
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetConditionAssessment>(
                Builders<AssetConditionAssessment>.IndexKeys
                    .Ascending(assessment => assessment.AssetRegistrationId)
                    .Descending(assessment => assessment.AssessmentDate),
                new CreateIndexOptions { Name = "ix_asset_condition_assessment_asset_registration_id_assessment_date" }),
            new CreateIndexModel<AssetConditionAssessment>(
                Builders<AssetConditionAssessment>.IndexKeys.Descending(assessment => assessment.AssessmentDate),
                new CreateIndexOptions { Name = "ix_asset_condition_assessment_assessment_date" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
