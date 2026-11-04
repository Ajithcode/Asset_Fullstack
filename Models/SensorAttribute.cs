namespace WorkerService.Models;
using MongoDB.Bson;
using System.Text.Json.Serialization;
public class SensorAttributes
{
    public string device_position { get; set; }

    public string person_code { get; set; }

    public string person_id { get; set; }

    public string person_name { get; set; }

    public string person_type { get; set; }

    public string access_status { get; set; }

    public string card_number { get; set; }

    public string device_ip { get; set; }

    public string status { get; set; }

    [JsonConverter(typeof(StringOrNumberConverter))]
    public string healthy_state { get; set; }

    public string device_trigger { get; set; }

    public string data_type { get; set; }

    public string cpu_utilisation { get; set; }

    public string memory_utilisation { get; set; }

    public string access_log_storage { get; set; }

    public string person_number_storage { get; set; }
}