using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace A360.Domain.Entities;

public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("client_id")]
    public string? ClientId { get; set; }

    [BsonElement("tenant_id")]
    public string? TenantId { get; set; }

    [BsonElement("created_by")]
    public string? CreatedBy { get; set; }

    [BsonElement("created_at")]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updated_by")]
    public string? UpdatedBy { get; set; }

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("is_deleted")]
    public bool IsDeleted { get; set; }
}
