using System.Linq.Expressions;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetRegistrationDraftRepository
    : MongoRepository<AssetRegistrationDraft>, IAssetRegistrationDraftRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetRegistrationDraft";

    public AssetRegistrationDraftRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetRegistrationDraft>(CollectionName))
    {
    }

    public async Task<AssetRegistrationDraft?> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(draft => draft.AssetRegistrationId == assetRegistrationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetRegistrationDraft>> GetRecentAsync(string? createdBy, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(createdBy)
            ? Builders<AssetRegistrationDraft>.Filter.Empty
            : Builders<AssetRegistrationDraft>.Filter.Eq(draft => draft.CreatedBy, createdBy);

        return await Collection
            .Find(filter)
            .SortByDescending(draft => draft.UpdatedAt)
            .ThenByDescending(draft => draft.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AssetTagExistsAsync(string assetTag, string? excludeAssetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(draft => draft.BasicInformation!.AssetTag == assetTag && draft.AssetRegistrationId != excludeAssetRegistrationId)
            .AnyAsync(cancellationToken);
    }

    public async Task<AssetRegistrationDraft?> UpdateSectionAsync<TSection>(
        string assetRegistrationId,
        Expression<Func<AssetRegistrationDraft, TSection?>> section,
        TSection value,
        int step,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<AssetRegistrationDraft>.Update
            .Set(section, value)
            .Max(draft => draft.CurrentStep, step)
            .Set(draft => draft.UpdatedBy, updatedBy)
            .Set(draft => draft.UpdatedAt, DateTime.UtcNow);

        return await Collection.FindOneAndUpdateAsync(
            draft => draft.AssetRegistrationId == assetRegistrationId,
            update,
            new FindOneAndUpdateOptions<AssetRegistrationDraft> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<bool> DeleteByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        var result = await Collection.DeleteOneAsync(draft => draft.AssetRegistrationId == assetRegistrationId, cancellationToken);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetRegistrationDraft>(
                Builders<AssetRegistrationDraft>.IndexKeys.Ascending(draft => draft.AssetRegistrationId),
                new CreateIndexOptions { Name = "ix_asset_registration_draft_asset_registration_id", Unique = true }),
            new CreateIndexModel<AssetRegistrationDraft>(
                Builders<AssetRegistrationDraft>.IndexKeys.Ascending(draft => draft.BasicInformation!.AssetTag),
                new CreateIndexOptions { Name = "ix_asset_registration_draft_asset_tag" }),
            new CreateIndexModel<AssetRegistrationDraft>(
                Builders<AssetRegistrationDraft>.IndexKeys
                    .Ascending(draft => draft.CreatedBy)
                    .Descending(draft => draft.UpdatedAt),
                new CreateIndexOptions { Name = "ix_asset_registration_draft_created_by_updated_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
