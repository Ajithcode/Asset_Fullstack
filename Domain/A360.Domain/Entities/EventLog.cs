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

    /// <summary>Extra detail, e.g. "assets.xlsx: 120 created, 4 updated, 2 failed" for an import.</summary>
    [BsonElement("details")]
    public string? Details { get; set; }

    // System Audit Log: request context captured when the event is written

    [BsonElement("performed_by_email")]
    public string? PerformedByEmail { get; set; }

    [BsonElement("performed_by_role")]
    public string? PerformedByRole { get; set; }

    [BsonElement("ip_address")]
    public string? IpAddress { get; set; }

    [BsonElement("user_agent")]
    public string? UserAgent { get; set; }

    [BsonElement("request_id")]
    public string? RequestId { get; set; }

    // System Audit Log: classification from AuditClassifier (filled in for older events by the Event Log service)

    /// <summary>One of <see cref="AuditCategory"/>: the tab the event shows under.</summary>
    [BsonElement("category")]
    public string? Category { get; set; }

    /// <summary>One of <see cref="AuditSeverity"/>.</summary>
    [BsonElement("severity")]
    public string? Severity { get; set; }

    /// <summary>1 (Low) to 4 (Critical), so the log can be sorted by severity.</summary>
    [BsonElement("severity_rank")]
    public int SeverityRank { get; set; }

    /// <summary>One of <see cref="AuditOutcome"/>.</summary>
    [BsonElement("outcome")]
    public string? Outcome { get; set; }

    /// <summary>Application module the event belongs to, e.g. "Assets", "Administration".</summary>
    [BsonElement("module")]
    public string? Module { get; set; }

    /// <summary><see cref="AuditClassifier.Version"/> the classification was made with; older ones are redone.</summary>
    [BsonElement("audit_version")]
    public int AuditVersion { get; set; }
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
    public const string TotpSetupStarted = "TotpSetupStarted";
    public const string TotpEnabled = "TotpEnabled";
    public const string TokenRefreshed = "TokenRefreshed";
    public const string TokenRefreshFailed = "TokenRefreshFailed";
    public const string TokenReuseDetected = "TokenReuseDetected";

    // Imports / exports (System Audit Log › Imports/Exports): one event per file, the record is the job or file
    public const string Imported = "Imported";
    public const string ImportFailed = "ImportFailed";
    public const string Exported = "Exported";
}
