using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Dashboard.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Dashboard : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("dashboard_id")]
    public string DashboardId { get; set; } = string.Empty;

    [BsonElement("dashboard_name")]
    public string DashboardName { get; set; } = string.Empty;
}
