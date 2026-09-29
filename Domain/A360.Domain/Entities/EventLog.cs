using MongoDB.Bson.Serialization.Attributes;

namespace A360.Domain.Entities;

public sealed class EventLog : BaseEntity
{
    [BsonElement("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [BsonElement("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [BsonElement("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [BsonElement("entity_name")]
    public string EntityName { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("performed_by")]
    public string? PerformedBy { get; set; }

    [BsonElement("performed_by_ref_id")]
    public string? PerformedByRefId { get; set; }
}

public static class EventAction
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Viewed = "Viewed";
    public const string LoggedIn = "LoggedIn";
    public const string LoggedOut = "LoggedOut";
    public const string LoginFailed = "LoginFailed";
    public const string LoginBlocked = "LoginBlocked";
    public const string OtpSent = "OtpSent";
    public const string OtpSendFailed = "OtpSendFailed";
    public const string OtpFailed = "OtpFailed";
    public const string OtpLocked = "OtpLocked";
    public const string TokenRefreshed = "TokenRefreshed";
    public const string TokenRefreshFailed = "TokenRefreshFailed";
    public const string TokenReuseDetected = "TokenReuseDetected";
}
