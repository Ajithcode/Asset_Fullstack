namespace WorkerService.Models;
using MongoDB.Bson;
public class RedisSensorMessage
{
    public string client_id { get; set; }

    public string macaddress { get; set; }

    public string gatewayid { get; set; }

    public string ant_id { get; set; }

    public string sensor_status { get; set; }

    public string device_id { get; set; }

    public string device_name { get; set; }

    public string description { get; set; }

    public string mfg_name { get; set; }

    public string device_type { get; set; }

    public DateTime? created_date { get; set; }

    public DateTime? created_date_utc { get; set; }

    public SensorAttributes sensor_attributes { get; set; }

    public string process_status { get; set; }
}