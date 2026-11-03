using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class Integration : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("integration_id")]
    public string IntegrationId { get; set; } = string.Empty;

    [BsonElement("integration_name")]
    public string IntegrationName { get; set; } = string.Empty;

    // Unique, upper-case code such as INT-ERP-001.
    [BsonElement("integration_code")]
    public string IntegrationCode { get; set; } = string.Empty;

    // External system, e.g. SAP ERP, Microsoft Entra ID.
    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    // Development, Staging or Production.
    [BsonElement("environment")]
    public string Environment { get; set; } = string.Empty;

    [BsonElement("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    // e.g. OAuth 2.0 (Client Credentials), API Key.
    [BsonElement("authentication_method")]
    public string AuthenticationMethod { get; set; } = string.Empty;

    // Client ID issued by the external system; named apart from BaseEntity.ClientId (the A360 client).
    [BsonElement("auth_client_id")]
    public string AuthClientId { get; set; } = string.Empty;

    // Encrypted at rest; never returned to clients.
    [BsonElement("secret_key_encrypted")]
    public string SecretKeyEncrypted { get; set; } = string.Empty;

    // e.g. Every 15 minutes.
    [BsonElement("sync_frequency")]
    public string SyncFrequency { get; set; } = string.Empty;

    [BsonElement("timeout_seconds")]
    public int TimeoutSeconds { get; set; }

    // e.g. 3 retries (exponential backoff).
    [BsonElement("retry_policy")]
    public string RetryPolicy { get; set; } = string.Empty;
}
