using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class NotificationTemplate : BaseEntity
{
    [BsonElement("template_code")]
    public string TemplateCode { get; set; } = string.Empty;

    [BsonElement("template_name")]
    public string TemplateName { get; set; } = string.Empty;

    [BsonElement("event")]
    public string Event { get; set; } = string.Empty;

    [BsonElement("channel")]
    public string Channel { get; set; } = string.Empty;

    [BsonElement("subject")]
    public string Subject { get; set; } = string.Empty;

    [BsonElement("message_body")]
    public string MessageBody { get; set; } = string.Empty;

    [BsonElement("recipients")]
    public List<string> Recipients { get; set; } = [];

    [BsonElement("cc_recipients")]
    public List<string> CcRecipients { get; set; } = [];

    [BsonElement("escalation_recipients")]
    public List<string> EscalationRecipients { get; set; } = [];

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
