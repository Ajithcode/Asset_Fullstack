using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace A360.Asset.Repository.Repositories;

/// <summary>Finds work orders and spare parts by their numbers, so service requests can link to them.</summary>
public interface IMaintenanceLookupRepository
{
    /// <param name="workOrderIds">Work order numbers, e.g. "WO-2025-076".</param>
    Task<IReadOnlyCollection<MaintenanceWorkOrderLookup>> FindWorkOrdersAsync(IReadOnlyCollection<string> workOrderIds, CancellationToken cancellationToken = default);

    /// <param name="partIds">Part numbers, e.g. "SP-0456".</param>
    Task<IReadOnlyCollection<MaintenanceSparePartLookup>> FindSparePartsAsync(IReadOnlyCollection<string> partIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only view of the Maintenance service's work_orders and spare_parts collections, which share this
/// service's database. Only the fields shown on a service request are read; deleted records are left out.
/// </summary>
public sealed class MaintenanceLookupRepository(IMongoDatabase database) : IMaintenanceLookupRepository
{
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public Task<IReadOnlyCollection<MaintenanceWorkOrderLookup>> FindWorkOrdersAsync(IReadOnlyCollection<string> workOrderIds, CancellationToken cancellationToken = default) =>
        FindAsync<MaintenanceWorkOrderLookup>("work_orders", "work_order_id", workOrderIds, cancellationToken);

    public Task<IReadOnlyCollection<MaintenanceSparePartLookup>> FindSparePartsAsync(IReadOnlyCollection<string> partIds, CancellationToken cancellationToken = default) =>
        FindAsync<MaintenanceSparePartLookup>("spare_parts", "part_id", partIds, cancellationToken);

    private async Task<IReadOnlyCollection<TLookup>> FindAsync<TLookup>(
        string collectionName,
        string numberField,
        IReadOnlyCollection<string> numbers,
        CancellationToken cancellationToken)
    {
        if (numbers.Count == 0)
        {
            return [];
        }

        var filter = Builders<TLookup>.Filter.And(
            Builders<TLookup>.Filter.Ne("is_deleted", true),
            Builders<TLookup>.Filter.In(numberField, numbers));

        return await database
            .GetCollection<TLookup>(collectionName)
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .ToListAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public sealed class MaintenanceWorkOrderLookup
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("work_order_id")]
    public string WorkOrderId { get; set; } = string.Empty;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("title")]
    public string? Title { get; set; }

    [BsonElement("work_type")]
    public string? WorkType { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("scheduled_date")]
    public DateTime? ScheduledDate { get; set; }

    [BsonElement("assigned_technician")]
    public string? AssignedTechnician { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MaintenanceSparePartLookup
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("part_id")]
    public string PartId { get; set; } = string.Empty;

    [BsonElement("part_name")]
    public string? PartName { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("quantity_in_stock")]
    public int QuantityInStock { get; set; }
}
