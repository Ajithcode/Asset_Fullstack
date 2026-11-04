using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetDedupRulesRepository
    : MongoRepository<AssetDedupRules>, IAssetDedupRulesRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetDedupRules";

    public AssetDedupRulesRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetDedupRules>(CollectionName))
    {
    }

    public async Task<AssetDedupRules?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(rules => !rules.IsDeleted)
            .SortByDescending(rules => rules.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        // A single small document: no indexes needed.
        return Task.CompletedTask;
    }
}
