using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A user record in this service. It is separate from the UserAccount users that log in.
public class SystemUser : BaseEntity
{
    // e.g. "USR-001".
    [BsonElement("code")]
    public string? Code { get; set; }

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

    // Further roles in this service the user holds besides the primary one.
    [BsonElement("additional_roles")]
    public List<MasterDataReference> AdditionalRoles { get; set; } = [];

    // Id of the linked employee, if any.
    [BsonElement("employee_id")]
    public string? EmployeeId { get; set; }

    // Names of the sites the user can access.
    [BsonElement("sites")]
    public List<string> Sites { get; set; } = [];

    // Date the account stops working; null for no expiry.
    [BsonElement("account_expiry")]
    public DateTime? AccountExpiry { get; set; }

    [BsonElement("mfa_required")]
    public bool MfaRequired { get; set; }

    [BsonElement("must_change_password")]
    public bool MustChangePassword { get; set; }

    // Whether an invitation should be sent to the user.
    [BsonElement("send_invite")]
    public bool SendInvite { get; set; }

    [BsonElement("language")]
    public string? Language { get; set; }

    [BsonElement("time_zone")]
    public string? TimeZone { get; set; }

    [BsonElement("date_format")]
    public string? DateFormat { get; set; }

    [BsonElement("email_notifications")]
    public bool EmailNotifications { get; set; } = true;

    [BsonElement("sms_notifications")]
    public bool SmsNotifications { get; set; }

    // Set by the sign-in flow; not editable through this API.
    [BsonElement("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
}
