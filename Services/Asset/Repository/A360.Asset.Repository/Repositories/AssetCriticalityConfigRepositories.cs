using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

/// <summary>Shared client scoping for the criticality configuration collections.</summary>
internal static class ConfigFilter
{
    public static FilterDefinition<T> For<T>(string? clientId) where T : BaseEntity =>
        Builders<T>.Filter.Eq(x => x.ClientId, clientId) & Builders<T>.Filter.Ne(x => x.IsDeleted, true);
}

public sealed class AssetCriticalityCriteriaRepository(IMongoDatabase database)
    : MongoRepository<AssetCriticalityCriterionMaster>(database.GetCollection<AssetCriticalityCriterionMaster>(CollectionName)), IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCriticalityCriteria";

    public async Task<List<AssetCriticalityCriterionMaster>> ListAsync(string? clientId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var filter = ConfigFilter.For<AssetCriticalityCriterionMaster>(clientId);
        if (!includeInactive)
        {
            filter &= Builders<AssetCriticalityCriterionMaster>.Filter.Eq(x => x.IsActive, true);
        }

        return await Collection.Find(filter).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<AssetCriticalityCriterionMaster?> GetByCodeOrNameAsync(string? clientId, string code, string name, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetCriticalityCriterionMaster>.Filter;
        var match = builder.Regex(x => x.Code, ExactIgnoreCase(code)) | builder.Regex(x => x.Name, ExactIgnoreCase(name));
        return await Collection.Find(ConfigFilter.For<AssetCriticalityCriterionMaster>(clientId) & match).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<long> CountAsync(string? clientId, CancellationToken cancellationToken = default) =>
        Collection.CountDocumentsAsync(ConfigFilter.For<AssetCriticalityCriterionMaster>(clientId), cancellationToken: cancellationToken);

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AssetCriticalityCriterionMaster>(
                Builders<AssetCriticalityCriterionMaster>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.Code),
                new CreateIndexOptions<AssetCriticalityCriterionMaster> { Name = "ix_criticality_criteria_client_code", Unique = true, PartialFilterExpression = new BsonDocumentFilterDefinition<AssetCriticalityCriterionMaster>(new MongoDB.Bson.BsonDocument("is_deleted", false)) }),
            new CreateIndexModel<AssetCriticalityCriterionMaster>(
                Builders<AssetCriticalityCriterionMaster>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.IsActive).Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_criticality_criteria_client_active_order" })
        ], cancellationToken);
    }

    internal static MongoDB.Bson.BsonRegularExpression ExactIgnoreCase(string value) =>
        new($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
}

public sealed class AssetCriticalityTemplateRepository(IMongoDatabase database)
    : MongoRepository<AssetCriticalityTemplate>(database.GetCollection<AssetCriticalityTemplate>(CollectionName)), IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCriticalityTemplate";

    /// <summary>The version in use of every template.</summary>
    public async Task<List<AssetCriticalityTemplate>> ListLatestAsync(string? clientId, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(ConfigFilter.For<AssetCriticalityTemplate>(clientId) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.IsLatest, true))
            .SortByDescending(x => x.IsDefault).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<AssetCriticalityTemplate>> GetVersionsAsync(string? clientId, string code, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(ConfigFilter.For<AssetCriticalityTemplate>(clientId) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.Code, code))
            .SortByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

    public async Task<AssetCriticalityTemplate?> GetLatestByCodeOrNameAsync(string? clientId, string code, string name, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetCriticalityTemplate>.Filter;
        var match = builder.Regex(x => x.Code, AssetCriticalityCriteriaRepository.ExactIgnoreCase(code))
            | builder.Regex(x => x.Name, AssetCriticalityCriteriaRepository.ExactIgnoreCase(name));
        return await Collection
            .Find(ConfigFilter.For<AssetCriticalityTemplate>(clientId) & builder.Eq(x => x.IsLatest, true) & match)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AssetCriticalityTemplate?> GetDefaultAsync(string? clientId, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(ConfigFilter.For<AssetCriticalityTemplate>(clientId) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.IsLatest, true) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.IsDefault, true))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Saves the new version and retires the old one's IsLatest / IsDefault flags (the old version stays for its assessments).</summary>
    public async Task SupersedeAsync(string? clientId, string code, string exceptId, CancellationToken cancellationToken = default) =>
        await Collection.UpdateManyAsync(
            ConfigFilter.For<AssetCriticalityTemplate>(clientId) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.Code, code) & Builders<AssetCriticalityTemplate>.Filter.Ne(x => x.Id, exceptId),
            Builders<AssetCriticalityTemplate>.Update.Set(x => x.IsLatest, false).Set(x => x.IsDefault, false),
            cancellationToken: cancellationToken);

    /// <summary>At most one template is the default: clears the flag on every other template.</summary>
    public async Task ClearDefaultAsync(string? clientId, string exceptCode, CancellationToken cancellationToken = default) =>
        await Collection.UpdateManyAsync(
            ConfigFilter.For<AssetCriticalityTemplate>(clientId) & Builders<AssetCriticalityTemplate>.Filter.Ne(x => x.Code, exceptCode) & Builders<AssetCriticalityTemplate>.Filter.Eq(x => x.IsDefault, true),
            Builders<AssetCriticalityTemplate>.Update.Set(x => x.IsDefault, false),
            cancellationToken: cancellationToken);

    public Task<long> CountAsync(string? clientId, CancellationToken cancellationToken = default) =>
        Collection.CountDocumentsAsync(ConfigFilter.For<AssetCriticalityTemplate>(clientId), cancellationToken: cancellationToken);

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AssetCriticalityTemplate>(
                Builders<AssetCriticalityTemplate>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.Code).Ascending(x => x.Version),
                new CreateIndexOptions { Name = "ix_criticality_template_client_code_version", Unique = true }),
            new CreateIndexModel<AssetCriticalityTemplate>(
                Builders<AssetCriticalityTemplate>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.IsLatest).Ascending(x => x.IsDefault),
                new CreateIndexOptions { Name = "ix_criticality_template_client_latest_default" })
        ], cancellationToken);
    }
}

public sealed class AssetCriticalityApprovalRuleRepository(IMongoDatabase database)
    : MongoRepository<AssetCriticalityApprovalRule>(database.GetCollection<AssetCriticalityApprovalRule>(CollectionName)), IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCriticalityApprovalRule";

    public async Task<List<AssetCriticalityApprovalRule>> ListAsync(string? clientId, CancellationToken cancellationToken = default) =>
        await Collection.Find(ConfigFilter.For<AssetCriticalityApprovalRule>(clientId)).SortBy(x => x.SortOrder).ToListAsync(cancellationToken);

    public async Task<AssetCriticalityApprovalRule?> GetByScenarioAsync(string? clientId, string scenario, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(ConfigFilter.For<AssetCriticalityApprovalRule>(clientId) & Builders<AssetCriticalityApprovalRule>.Filter.Regex(x => x.Scenario, AssetCriticalityCriteriaRepository.ExactIgnoreCase(scenario)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateOneAsync(new CreateIndexModel<AssetCriticalityApprovalRule>(
            Builders<AssetCriticalityApprovalRule>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.Scenario),
            new CreateIndexOptions { Name = "ix_criticality_approval_rule_client_scenario", Unique = true }), cancellationToken: cancellationToken);
    }
}

public sealed class AssetCriticalityReassessmentRuleRepository(IMongoDatabase database)
    : MongoRepository<AssetCriticalityReassessmentRule>(database.GetCollection<AssetCriticalityReassessmentRule>(CollectionName)), IMongoIndexConfigurator
{
    public const string CollectionName = "AssetCriticalityReassessmentRule";

    public async Task<List<AssetCriticalityReassessmentRule>> ListAsync(string? clientId, CancellationToken cancellationToken = default) =>
        await Collection.Find(ConfigFilter.For<AssetCriticalityReassessmentRule>(clientId)).ToListAsync(cancellationToken);

    public async Task<AssetCriticalityReassessmentRule?> GetByLevelAsync(string? clientId, string levelName, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(ConfigFilter.For<AssetCriticalityReassessmentRule>(clientId) & Builders<AssetCriticalityReassessmentRule>.Filter.Regex(x => x.LevelName, AssetCriticalityCriteriaRepository.ExactIgnoreCase(levelName)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateOneAsync(new CreateIndexModel<AssetCriticalityReassessmentRule>(
            Builders<AssetCriticalityReassessmentRule>.IndexKeys.Ascending(x => x.ClientId).Ascending(x => x.LevelName),
            new CreateIndexOptions { Name = "ix_criticality_reassessment_rule_client_level", Unique = true }), cancellationToken: cancellationToken);
    }
}
