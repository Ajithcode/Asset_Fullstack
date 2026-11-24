using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Administration.Repository.Repositories;

/// <summary>A Tracking alert rule that watches a drawn zone.</summary>
/// <param name="TargetName">The rule's zone label, e.g. "Dock 1 · Floor 2".</param>
public sealed record ZoneAlertRule(string Id, string Name, string ZoneId, string? TargetName, string Status);

/// <summary>
/// Geofence &amp; Alerts rules (Tracking service) that watch Maps &amp; Floor Plans zones, so deleting a zone does not
/// leave its rules silently broken.
/// </summary>
/// <remarks>
/// Cross-service access: the rules live in Tracking's "AlertRule" collection, in the same Mongo database (Asset360) as
/// maps_zones. Tracking already reads maps_zones directly (MapZoneLookup); this is the mirror image. Only the fields
/// below are touched: read name / geofence_id / map_zone_id / status / is_deleted, and on an explicit
/// "delete and disable rules" set status = Disabled plus status_reason / updated_by / updated_at. A rule targets a zone
/// through map_zone_id = {zoneId} or geofence_id = "zone:{zoneId}" (AlertNotifyDefaults.MapZonePrefix in Tracking).
/// If Tracking ever moves to its own database, replace this with a call to the Tracking API.
/// </remarks>
public interface IZoneAlertRules
{
    /// <summary>Rules (not deleted) that watch any of the zones.</summary>
    Task<IReadOnlyList<ZoneAlertRule>> FindAsync(IReadOnlyCollection<string> zoneIds, CancellationToken cancellationToken = default);

    /// <summary>Sets the rules to Disabled with the reason (rules already disabled get the reason too).</summary>
    Task DisableAsync(IReadOnlyCollection<ZoneAlertRule> rules, Func<ZoneAlertRule, string> reason, string? updatedBy, CancellationToken cancellationToken = default);
}

public sealed class ZoneAlertRules(IMongoDatabase database) : IZoneAlertRules
{
    /// <summary>Tracking's alert rule collection (A360.Tracking: AlertRule entity).</summary>
    public const string CollectionName = "AlertRule";
    public const string MapZonePrefix = "zone:";
    public const string Active = "Active";
    public const string Disabled = "Disabled";

    private IMongoCollection<BsonDocument> Rules => database.GetCollection<BsonDocument>(CollectionName);

    public async Task<IReadOnlyList<ZoneAlertRule>> FindAsync(IReadOnlyCollection<string> zoneIds, CancellationToken cancellationToken = default)
    {
        var ids = zoneIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var f = Builders<BsonDocument>.Filter;
        var filter = f.Ne("is_deleted", true)
            & f.Or(f.In("map_zone_id", ids), f.In("geofence_id", ids.Select(id => MapZonePrefix + id)));
        var docs = await Rules.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("name").Include("geofence_id").Include("geofence_name").Include("map_zone_id").Include("status"))
            .ToListAsync(cancellationToken);

        return docs.Select(doc =>
        {
            var geofenceId = Text(doc, "geofence_id");
            var zoneId = Text(doc, "map_zone_id")
                ?? (geofenceId is not null && geofenceId.StartsWith(MapZonePrefix, StringComparison.Ordinal) ? geofenceId[MapZonePrefix.Length..] : string.Empty);
            return new ZoneAlertRule(doc["_id"].ToString()!, Text(doc, "name") ?? "(unnamed rule)", zoneId, Text(doc, "geofence_name"), Text(doc, "status") ?? Active);
        }).OrderBy(rule => rule.Name).ToList();
    }

    public async Task DisableAsync(IReadOnlyCollection<ZoneAlertRule> rules, Func<ZoneAlertRule, string> reason, string? updatedBy, CancellationToken cancellationToken = default)
    {
        if (rules.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var writes = rules.Select(rule => new UpdateOneModel<BsonDocument>(
            Builders<BsonDocument>.Filter.Eq("_id", IdValue(rule.Id)),
            Builders<BsonDocument>.Update
                .Set("status", Disabled)
                .Set("status_reason", reason(rule))
                .Set("updated_by", BsonValue.Create(updatedBy))
                .Set("updated_at", now)))
            .ToList();

        await Rules.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
    }

    /// <summary>AlertRule ids are ObjectIds (BaseEntity), stored as such.</summary>
    private static BsonValue IdValue(string id) => ObjectId.TryParse(id, out var objectId) ? objectId : id;

    private static string? Text(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString) ? value.AsString : null;
}
