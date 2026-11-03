using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Account status (Active, Pending, Suspended) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class User : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("user_id")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    // URL of the profile picture, typically returned by the Media service upload.
    [BsonElement("image")]
    public string? Image { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("employee_id")]
    public string? EmployeeId { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("job_title")]
    public string? JobTitle { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("time_zone")]
    public string? TimeZone { get; set; }

    [BsonElement("primary_role")]
    public string PrimaryRole { get; set; } = string.Empty;

    [BsonElement("secondary_roles")]
    public List<string> SecondaryRoles { get; set; } = [];

    // "All Locations" or a specific location.
    [BsonElement("access_scope")]
    public string? AccessScope { get; set; }

    [BsonElement("mfa_enabled")]
    public bool MfaEnabled { get; set; }

    [BsonElement("module_access")]
    public List<string> ModuleAccess { get; set; } = [];

    [BsonElement("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
}
