using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.MasterDataManagement.Api.Endpoints;

/// <summary>
/// Read-only view of the Asset service's category masters (same database): the categories, sub-categories
/// (categories with a parent) and asset types that Asset Registration offers. Asset models reference these.
/// </summary>
public static class AssetCatalogLookup
{
    private const string Categories = "AssetCategory";
    private const string Types = "AssetCategoryType";

    public sealed record CatalogCategory(string Id, string Name, string? ParentId);

    public sealed record CatalogType(string Id, string Name, string CategoryId);

    public static async Task<CatalogCategory?> FindCategoryAsync(IMongoDatabase database, string? id, CancellationToken cancellationToken)
    {
        var doc = await FindAsync(database, Categories, id, cancellationToken);
        return doc is null ? null : new CatalogCategory(doc["_id"].ToString()!, Text(doc, "name") ?? string.Empty, Text(doc, "parent_category_id"));
    }

    public static async Task<CatalogType?> FindTypeAsync(IMongoDatabase database, string? id, CancellationToken cancellationToken)
    {
        var doc = await FindAsync(database, Types, id, cancellationToken);
        return doc is null ? null : new CatalogType(doc["_id"].ToString()!, Text(doc, "type_name") ?? string.Empty, Text(doc, "category_id") ?? string.Empty);
    }

    private static async Task<BsonDocument?> FindAsync(IMongoDatabase database, string collection, string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out var objectId))
        {
            return null;
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId) & Builders<BsonDocument>.Filter.Ne("is_deleted", true);
        return await database.GetCollection<BsonDocument>(collection).Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull ? value.ToString() : null;
}
