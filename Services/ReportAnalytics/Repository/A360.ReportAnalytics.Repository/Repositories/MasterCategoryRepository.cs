using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.ReportAnalytics.Repository.Repositories;

/// <summary>The asset categories set up in Master Data Management, which report categories are picked from.</summary>
public interface IMasterCategoryRepository
{
    /// <summary>Names of the active top-level categories (sub categories left out), sorted.</summary>
    Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>The category's name as spelled in master data (matched ignoring case), or null when there is no such active category.</summary>
    Task<string?> FindCategoryAsync(string? name, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only view of the "AssetCategory" collection (the categories added under /api/asset-categories, Master Data
/// Management screen), which shares this service's database. Deleted and inactive categories are left out.
/// </summary>
public sealed class MasterCategoryRepository(IMongoDatabase database) : IMasterCategoryRepository
{
    private const string CollectionName = "AssetCategory";

    public async Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        var documents = await database
            .GetCollection<BsonDocument>(CollectionName)
            .Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true))
            .ToListAsync(cancellationToken);

        return documents
            .Where(doc => !IsInactive(doc) && !HasParent(doc))
            .Select(doc => Text(doc, "name"))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string?> FindCategoryAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var names = await GetCategoryNamesAsync(cancellationToken);
        return names.FirstOrDefault(item => string.Equals(item, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // Sub categories point at their parent through parent_category_id.
    private static bool HasParent(BsonDocument doc) => Text(doc, "parent_category_id") is not null;

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value is BsonString text && !string.IsNullOrWhiteSpace(text.Value) ? text.Value.Trim() : null;

    // Status is "Active"/"Inactive" text in most lists and a boolean in a few.
    private static bool IsInactive(BsonDocument doc) =>
        doc.TryGetValue("status", out var status) && (status is BsonBoolean { Value: false }
            || (status is BsonString text && string.Equals(text.Value, "Inactive", StringComparison.OrdinalIgnoreCase)));
}
