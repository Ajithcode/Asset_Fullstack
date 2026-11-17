using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Repository.Repositories;

/// <summary>An asset a tag can be linked to: its registration id, its own code (asset tag) and name.</summary>
public sealed record TagAsset(string AssetRegistrationId, string? AssetCode, string? AssetName);

public interface ITagAssetLookup
{
    /// <summary>The asset whose registration id (AR-000123) or asset code (FL-008) is given, ignoring case; null when none.</summary>
    Task<TagAsset?> FindAsync(string registrationIdOrCode, CancellationToken cancellationToken = default);
}

/// <summary>Reads assets registered by the Asset service from the shared database.</summary>
public sealed class TagAssetLookup(IMongoDatabase database) : ITagAssetLookup
{
    private const string AssetRegistrationCollectionName = "AssetRegistration";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public async Task<TagAsset?> FindAsync(string registrationIdOrCode, CancellationToken cancellationToken = default)
    {
        var key = registrationIdOrCode.Trim();
        var filter = Builders<BsonDocument>.Filter;

        var asset = await database.GetCollection<BsonDocument>(AssetRegistrationCollectionName)
            .Find(filter.Ne("is_deleted", true)
                & filter.Or(filter.Eq("asset_registration_id", key), filter.Eq("basic_information.asset_tag", key)),
                new FindOptions { Collation = CaseInsensitive })
            .Project(Builders<BsonDocument>.Projection.Include("asset_registration_id").Include("basic_information"))
            .FirstOrDefaultAsync(cancellationToken);

        if (asset is null || !asset.TryGetValue("asset_registration_id", out var id) || !id.IsString)
        {
            return null;
        }

        var basic = asset.TryGetValue("basic_information", out var info) && info.IsBsonDocument ? info.AsBsonDocument : null;
        return new TagAsset(id.AsString, Text(basic, "asset_tag"), Text(basic, "asset_name"));
    }

    private static string? Text(BsonDocument? document, string field)
    {
        return document is not null && document.TryGetValue(field, out var value) && value.IsString
            && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString.Trim()
            : null;
    }
}
