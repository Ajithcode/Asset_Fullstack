using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive, Draft) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class NotificationTemplate : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [BsonElement("template_name")]
    public string TemplateName { get; set; } = string.Empty;

    // Unique, upper-case code such as NT-WO-001.
    [BsonElement("template_code")]
    public string TemplateCode { get; set; } = string.Empty;

    // Module the template belongs to, e.g. Maintenance, Compliance.
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    // Event that fires the notification, e.g. Work Order Assigned.
    [BsonElement("trigger_event")]
    public string TriggerEvent { get; set; } = string.Empty;

    // Email, SMS, In-App, Push.
    [BsonElement("delivery_channels")]
    public List<string> DeliveryChannels { get; set; } = [];

    [BsonElement("language")]
    public string Language { get; set; } = string.Empty;

    [BsonElement("priority")]
    public string? Priority { get; set; }

    // e.g. Send Immediately.
    [BsonElement("delivery_timing")]
    public string? DeliveryTiming { get; set; }

    // e.g. 3 retries (1hr apart).
    [BsonElement("retry_policy")]
    public string? RetryPolicy { get; set; }
}
