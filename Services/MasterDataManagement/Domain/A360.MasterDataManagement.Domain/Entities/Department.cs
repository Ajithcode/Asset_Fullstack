using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// Ignores the "employees" array older records still carry: employees are now built from the Employee master.
[BsonIgnoreExtraElements]
public class Department : BaseEntity
{
    [BsonElement("department_name")]
    public string DepartmentName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a business unit in this service.
    [BsonElement("business_unit")]
    public MasterDataReference? BusinessUnit { get; set; }

    // An employee in this service when it has an id; a name-only head is kept as typed.
    [BsonElement("department_head")]
    public MasterDataReference? DepartmentHead { get; set; }

    // Assets live in the Asset service, so these are stored as given.
    [BsonElement("assets")]
    public List<MasterDataReference> Assets { get; set; } = [];
}
