namespace WorkerService.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
public class SensorHistory
{
    // Omitted from the BSON document when left unset (ObjectId.Empty) so
    // MongoDB assigns a fresh _id on insert, and a replace against an
    // existing document (matched by stream_entry_id) keeps that document's
    // original _id instead of colliding on the immutable _id field.
    [BsonIgnoreIfDefault]
    public ObjectId Id { get; set; }

    public string stream_entry_id { get; set; }

    public ObjectId mydevice_id { get; set; }

    public string model_id { get; set; }

    public string type { get; set; }

    public string unique_id { get; set; }

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

    public string macaddress { get; set; }

    public string gatewayid { get; set; }

    public string ant_id { get; set; }

    public string sensor_status { get; set; }

    public string device_id { get; set; }

    public string device_name { get; set; }

    public string mfg_name { get; set; }

    public string device_type { get; set; }

    public SensorAttributes sensor_attributes { get; set; }

    public DateTime read_date { get; set; }

    public DateTime created_date { get; set; }

    public DateTime created_date_utc { get; set; }
}