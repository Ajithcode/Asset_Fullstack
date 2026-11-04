using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace WorkerService.Models;
using MongoDB.Bson;


[BsonIgnoreExtraElements]

public class DeviceMaster
{
    public ObjectId Id { get; set; }

    public string unique_id { get; set; }

    public string model_id { get; set; }

    public string type { get; set; }

    public ObjectId project_id { get; set; }

    public string project_name { get; set; }

    public ObjectId building_id { get; set; }

    public string building_name { get; set; }

    public ObjectId floor_id { get; set; }

    public string floor_name { get; set; }

    public ObjectId area_id { get; set; }

    public string area_name { get; set; }

    public ObjectId zone_id { get; set; }

    public string zone_name { get; set; }

    public ObjectId country_id { get; set; }

    public string country_name { get; set; }

    public string description { get; set; }

    public List<string> module { get; set; }
}