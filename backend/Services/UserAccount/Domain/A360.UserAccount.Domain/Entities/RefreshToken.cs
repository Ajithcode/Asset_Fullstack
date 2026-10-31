using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.UserAccount.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class RefreshToken : BaseEntity
{
    [BsonElement("user_ref_id")]
    public string UserRefId { get; set; } = string.Empty;

    [BsonElement("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [BsonElement("replaced_by_token_hash")]
    public string? ReplacedByTokenHash { get; set; }
}
