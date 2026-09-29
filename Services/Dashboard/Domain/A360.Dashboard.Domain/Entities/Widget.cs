using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Dashboard.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Widget : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("widget_id")]
    public string WidgetId { get; set; } = string.Empty;

    // References Dashboard.DashboardId (not the dashboard's Mongo _id).
    [BsonElement("dashboard_id")]
    public string DashboardId { get; set; } = string.Empty;

    [BsonElement("module_name")]
    public string ModuleName { get; set; } = string.Empty;

    [BsonElement("widget_list")]
    public List<string> WidgetList { get; set; } = [];
}
