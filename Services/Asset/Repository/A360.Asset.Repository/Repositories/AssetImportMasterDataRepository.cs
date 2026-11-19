using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Asset.Repository.Repositories;

/// <summary>Reads the master lists that bulk import values are checked against.</summary>
public interface IAssetImportMasterDataRepository
{
    Task<AssetImportMasterData> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only view of the MasterDataManagement collections (which share this service's database): the lists the
/// registration screens offer for category, asset type, criticality, condition, department, business unit and location.
/// Deleted records are left out; inactive ones are kept so they can be reported as inactive.
/// </summary>
public sealed class AssetImportMasterDataRepository(IMongoDatabase database) : IAssetImportMasterDataRepository
{
    public async Task<AssetImportMasterData> LoadAsync(CancellationToken cancellationToken = default) => new(
        Categories: await LoadAsync("master_asset_categories", "category_name", doc => Ref(doc, "parent_category"), cancellationToken),
        AssetTypes: await LoadAsync("master_asset_types", "asset_type_name", doc => Ref(doc, "category"), cancellationToken),
        Criticalities: await LoadAsync("master_criticalities", "criticality_name", null, cancellationToken),
        Conditions: await LoadAsync("master_conditions", "condition_name", null, cancellationToken),
        Departments: await LoadAsync("master_departments", "department_name", null, cancellationToken),
        BusinessUnits: await LoadAsync("master_business_units", "business_unit_name", null, cancellationToken),
        Sites: await LoadAsync("master_sites", "site_name", null, cancellationToken),
        Buildings: await LoadAsync("master_buildings", "name", doc => Ref(doc, "site"), cancellationToken),
        Floors: await LoadAsync("master_floors", "name", doc => (Text(doc, "building_id"), Text(doc, "building_name")), cancellationToken),
        Zones: await LoadAsync("master_zones", "name", doc => (Text(doc, "floor_id"), Text(doc, "floor_name")), cancellationToken));

    private async Task<IReadOnlyList<AssetImportMasterItem>> LoadAsync(
        string collectionName,
        string nameField,
        Func<BsonDocument, (string? Id, string? Name)>? parent,
        CancellationToken cancellationToken)
    {
        var documents = await database
            .GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Ne("is_deleted", true))
            .ToListAsync(cancellationToken);

        return documents
            .Select(doc =>
            {
                var (parentId, parentName) = parent?.Invoke(doc) ?? (null, null);
                return new AssetImportMasterItem(
                    doc.GetValue("_id", BsonNull.Value).ToString()!,
                    Text(doc, nameField) ?? string.Empty,
                    Text(doc, "code"),
                    IsInactive(doc),
                    parentId,
                    parentName,
                    Text(doc, "country"));
            })
            .Where(item => item.Name.Length > 0)
            .ToList();
    }

    private static (string? Id, string? Name) Ref(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value is BsonDocument reference
            ? (Text(reference, "id"), Text(reference, "name"))
            : (null, null);

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value is BsonString text && !string.IsNullOrWhiteSpace(text.Value) ? text.Value.Trim() : null;

    // Status is "Active"/"Inactive" text in most lists and a boolean in a few.
    private static bool IsInactive(BsonDocument doc) =>
        doc.TryGetValue("status", out var status) && (status is BsonBoolean { Value: false }
            || (status is BsonString text && string.Equals(text.Value, "Inactive", StringComparison.OrdinalIgnoreCase)));
}

/// <param name="ParentId">Category: the parent category (set for sub categories). Asset type: its category.
/// Building: its site. Floor: its building. Zone: its floor.</param>
/// <param name="Country">Sites only.</param>
public sealed record AssetImportMasterItem(
    string Id,
    string Name,
    string? Code,
    bool Inactive,
    string? ParentId,
    string? ParentName,
    string? Country);

/// <summary>Each list is empty when it has not been set up; values for an empty list are not checked.</summary>
public sealed record AssetImportMasterData(
    IReadOnlyList<AssetImportMasterItem> Categories,
    IReadOnlyList<AssetImportMasterItem> AssetTypes,
    IReadOnlyList<AssetImportMasterItem> Criticalities,
    IReadOnlyList<AssetImportMasterItem> Conditions,
    IReadOnlyList<AssetImportMasterItem> Departments,
    IReadOnlyList<AssetImportMasterItem> BusinessUnits,
    IReadOnlyList<AssetImportMasterItem> Sites,
    IReadOnlyList<AssetImportMasterItem> Buildings,
    IReadOnlyList<AssetImportMasterItem> Floors,
    IReadOnlyList<AssetImportMasterItem> Zones);
