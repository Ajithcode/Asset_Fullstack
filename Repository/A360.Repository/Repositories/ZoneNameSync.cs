using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Repository.Repositories;

/// <summary>
/// After a Master Data zone is renamed: keeps the STORED zone-name copies in step (they are only fallbacks, readers resolve the
/// name from master_zones through <see cref="ZoneNames"/>). Touches floor-plan zones (maps_zones), plots (maps_plots), the worker's
/// TagLocationState, asset registrations and the geofence name on alerts / alert rules. Never writes to Master Data.
/// </summary>
public static class ZoneNameSync
{
    public static async Task<IReadOnlyDictionary<string, long>> RenameAsync(IMongoDatabase db, string masterZoneId, string? oldName, string newName, CancellationToken ct = default)
    {
        var counts = new Dictionary<string, long>();
        if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrEmpty(oldName) || oldName == newName) return counts;

        BsonValue Path(string field) => new BsonDocument("$ifNull", new BsonArray { "$" + field, "" });
        BsonValue SwapTail(string field) => new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$regexMatch", new BsonDocument { { "input", Path(field) }, { "regex", Regex.Escape(oldName) + "$" } }),
            new BsonDocument("$concat", new BsonArray
            {
                new BsonDocument("$substrCP", new BsonArray { Path(field), 0, new BsonDocument("$subtract", new BsonArray { new BsonDocument("$strLenCP", Path(field)), oldName.Length }) }),
                newName
            }),
            "$" + field
        });
        UpdateDefinition<BsonDocument> Pipeline(BsonDocument set) => new PipelineUpdateDefinition<BsonDocument>(new[] { new BsonDocument("$set", set) });

        // floor-plan zones: polygon name + the Master Data location copy
        var zones = db.GetCollection<BsonDocument>("maps_zones");
        var byMaster = Builders<BsonDocument>.Filter.Eq("location.zone_id", masterZoneId);
        var polygonIds = (await zones.Find(byMaster).Project(new BsonDocument("zone_id", 1)).ToListAsync(ct))
            .Select(d => d.GetValue("zone_id", BsonNull.Value)).Where(v => v.IsString).Select(v => v.AsString).ToList();
        counts["maps_zones"] = (await zones.UpdateManyAsync(byMaster,
            Pipeline(new BsonDocument { { "name", newName }, { "location.zone_name", newName }, { "location.location_path", SwapTail("location.location_path") } }), cancellationToken: ct)).ModifiedCount;

        // plots (zone_id = polygon id, location.zone_id = Master Data id)
        counts["maps_plots"] = (await db.GetCollection<BsonDocument>("maps_plots").UpdateManyAsync(byMaster,
            Pipeline(new BsonDocument { { "zone_name", newName }, { "location.zone_name", newName }, { "location.location_path", SwapTail("location.location_path") } }), cancellationToken: ct)).ModifiedCount;

        if (polygonIds.Count > 0)
        {
            counts["TagLocationState"] = (await db.GetCollection<BsonDocument>("TagLocationState").UpdateManyAsync(
                Builders<BsonDocument>.Filter.In("zone_id", polygonIds), Builders<BsonDocument>.Update.Set("zone_name", newName), cancellationToken: ct)).ModifiedCount;

            // indoor alerts / rules: "Name · Floor" with geofence id "zone:<polygon id>"
            var geoIds = polygonIds.Select(p => "zone:" + p).ToList();
            var geoName = new BsonDocument("$cond", new BsonArray
            {
                new BsonDocument("$regexMatch", new BsonDocument { { "input", Path("geofence_name") }, { "regex", "^" + Regex.Escape(oldName) } }),
                new BsonDocument("$concat", new BsonArray { newName, new BsonDocument("$substrCP", new BsonArray { Path("geofence_name"), oldName.Length, 1000 }) }),
                "$geofence_name"
            });
            foreach (var collection in new[] { "IoTAlert", "AlertRule" })
            {
                counts[collection] = (await db.GetCollection<BsonDocument>(collection).UpdateManyAsync(
                    Builders<BsonDocument>.Filter.In("geofence_id", geoIds), Pipeline(new BsonDocument("geofence_name", geoName)), cancellationToken: ct)).ModifiedCount;
            }
        }

        // asset registrations placed in this zone
        counts["AssetRegistration"] = (await db.GetCollection<BsonDocument>("AssetRegistration").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("location_assignment.location.zone_id", masterZoneId),
            Pipeline(new BsonDocument { { "location_assignment.location.zone_name", newName }, { "location_assignment.location.location_path", SwapTail("location_assignment.location.location_path") } }),
            cancellationToken: ct)).ModifiedCount;

        ZoneNames.Invalidate();
        return counts;
    }
}
