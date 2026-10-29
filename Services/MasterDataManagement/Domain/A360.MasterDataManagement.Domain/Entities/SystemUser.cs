using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A user record in this service. It is separate from the UserAccount users that log in.
public class SystemUser : BaseEntity
{
    [BsonElement("full_name")]
    public string FullName { get; set; } = null!;

    [BsonElement("username")]
    public string Username { get; set; } = null!;

    [BsonElement("email")]
    public string Email { get; set; } = null!;

    // Points at a department in this service.
    [BsonElement("department")]
    public MasterDataReference? Department { get; set; }

    // Points at a role in this service.
    [BsonElement("primary_role")]
    public MasterDataReference? PrimaryRole { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    // URL of a photo uploaded through /api/media/upload.
    [BsonElement("photo_url")]
    public string? PhotoUrl { get; set; }
}
