using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Employee : BaseEntity
{
    [BsonElement("employee_name")]
    public string EmployeeName { get; set; } = null!;

    // The organisation's own employee number, e.g. "EMP-0042"; not the record id.
    [BsonElement("employee_id")]
    public string EmployeeId { get; set; } = null!;

    // Points at a department in this service. This is what a department's employee list is built from.
    [BsonElement("department")]
    public MasterDataReference? Department { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("phone_number")]
    public string? PhoneNumber { get; set; }

    // Profile photo as an image URL or data URL.
    [BsonElement("photo")]
    public string? Photo { get; set; }

    [BsonElement("employment_type")]
    public string? EmploymentType { get; set; }

    [BsonElement("joining_date")]
    public DateOnly? JoiningDate { get; set; }

    // Site name as picked on the form.
    [BsonElement("site")]
    public string? Site { get; set; }

    // Points at another employee in this service.
    [BsonElement("reporting_manager")]
    public MasterDataReference? ReportingManager { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("alternate_phone")]
    public string? AlternatePhone { get; set; }

    [BsonElement("emergency_contact_name")]
    public string? EmergencyContactName { get; set; }

    [BsonElement("emergency_contact_phone")]
    public string? EmergencyContactPhone { get; set; }

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("nationality")]
    public string? Nationality { get; set; }

    // Skill names from the skill master.
    [BsonElement("skills")]
    public List<string> Skills { get; set; } = [];

    [BsonElement("certifications")]
    public List<PersonCertification> Certifications { get; set; } = [];

    [BsonElement("access_role")]
    public string? AccessRole { get; set; }

    [BsonElement("can_approve")]
    public bool CanApprove { get; set; }

    [BsonElement("notify_email")]
    public bool NotifyEmail { get; set; }

    [BsonElement("notify_sms")]
    public bool NotifySms { get; set; }
}
