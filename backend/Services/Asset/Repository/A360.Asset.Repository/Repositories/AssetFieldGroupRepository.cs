using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetFieldGroupRepository
    : MongoRepository<AssetFieldGroup>, IAssetFieldGroupRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetFieldGroup";

    // Compares text ignoring case, for group name uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetFieldGroupRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetFieldGroup>(CollectionName))
    {
    }

    public async Task<AssetFieldGroup?> GetByNameAsync(string groupName, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(group => group.GroupName == groupName, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AssetFieldGroup>(
                Builders<AssetFieldGroup>.IndexKeys.Ascending(group => group.GroupName),
                new CreateIndexOptions { Name = "ix_asset_field_group_group_name", Unique = true, Collation = CaseInsensitive }),
            cancellationToken: cancellationToken);
    }
}
