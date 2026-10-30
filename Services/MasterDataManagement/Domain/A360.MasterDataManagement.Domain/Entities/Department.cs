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

    // Optional parent department (in this collection) for sub-departments.
    [BsonElement("parent_department")]
    public MasterDataReference? ParentDepartment { get; set; }

    // Built-in icon the UI shows when there is no logo.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("logo_url")]
    public string? LogoUrl { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("deputy")]
    public string? Deputy { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    // Country whose dial code prefixes Phone.
    [BsonElement("phone_country")]
    public string? PhoneCountry { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("annual_budget")]
    public decimal? AnnualBudget { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("gl_code")]
    public string? GlCode { get; set; }

    [BsonElement("approval_limit")]
    public decimal? ApprovalLimit { get; set; }

    [BsonElement("allow_asset_ownership")]
    public bool AllowAssetOwnership { get; set; }

    [BsonElement("require_assignment_approval")]
    public bool RequireAssignmentApproval { get; set; }

    [BsonElement("include_in_cost_allocation")]
    public bool IncludeInCostAllocation { get; set; }

    // 'yyyy-MM-dd'; EffectiveTo is optional.
    [BsonElement("effective_from")]
    public string? EffectiveFrom { get; set; }

    [BsonElement("effective_to")]
    public string? EffectiveTo { get; set; }
}
