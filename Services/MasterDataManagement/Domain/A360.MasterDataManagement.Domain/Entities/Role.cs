using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A role in this service. It is separate from the UserAccount roles that control login.
public class Role : BaseEntity
{
    [BsonElement("role_name")]
    public string RoleName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // "System Role" or "Custom Role" (older records may hold other values such as "Business").
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // Permissions in this service granted to the role.
    [BsonElement("permissions")]
    public List<MasterDataReference> Permissions { get; set; } = [];

    // Modules in this service the role can see.
    // Kept in sync with ModulePermissions: every module where the role has at least one action.
    [BsonElement("module_access")]
    public List<MasterDataReference> ModuleAccess { get; set; } = [];

    // Permission matrix: View / Create / Edit / Delete / Approve / Export per module.
    [BsonElement("module_permissions")]
    public List<RoleModulePermission> ModulePermissions { get; set; } = [];

    // "All Locations", "Assigned Locations" or "Own Records".
    [BsonElement("data_scope")]
    public string DataScope { get; set; } = RoleDataScopes.AllLocations;

    // Used when DataScope is "Assigned Locations".
    [BsonElement("locations")]
    public List<MasterDataReference> Locations { get; set; } = [];

    [BsonElement("departments")]
    public List<MasterDataReference> Departments { get; set; } = [];

    // Highest amount this role can approve, in ApprovalCurrency. 0 means no approval authority by amount.
    [BsonElement("approval_limit")]
    public decimal ApprovalLimit { get; set; }

    [BsonElement("approval_currency")]
    public string ApprovalCurrency { get; set; } = "AED";

    [BsonElement("mfa_required")]
    public bool MfaRequired { get; set; }
}
