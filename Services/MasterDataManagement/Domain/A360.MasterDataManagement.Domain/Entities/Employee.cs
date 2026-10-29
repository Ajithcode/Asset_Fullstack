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
}
