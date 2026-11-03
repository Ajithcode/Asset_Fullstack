using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>One row of a role's permission matrix: what the role may do in one module.</summary>
public class RoleModulePermission
{
    [BsonElement("module_id")]
    public string ModuleId { get; set; } = null!;

    [BsonElement("module_name")]
    public string ModuleName { get; set; } = null!;

    [BsonElement("view")]
    public bool View { get; set; }

    [BsonElement("create")]
    public bool Create { get; set; }

    [BsonElement("edit")]
    public bool Edit { get; set; }

    [BsonElement("delete")]
    public bool Delete { get; set; }

    [BsonElement("approve")]
    public bool Approve { get; set; }

    [BsonElement("export")]
    public bool Export { get; set; }

    public bool HasAny() => View || Create || Edit || Delete || Approve || Export;
}

/// <summary>The actions a permission matrix row can grant.</summary>
public static class ModuleActions
{
    public const string View = "View";
    public const string Create = "Create";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Approve = "Approve";
    public const string Export = "Export";

    public static readonly IReadOnlyList<string> All = [View, Create, Edit, Delete, Approve, Export];
}

/// <summary>Values for Role.DataScope.</summary>
public static class RoleDataScopes
{
    public const string AllLocations = "All Locations";
    public const string AssignedLocations = "Assigned Locations";
    public const string OwnRecords = "Own Records";

    public static readonly IReadOnlyList<string> All = [AllLocations, AssignedLocations, OwnRecords];
}

/// <summary>Values for Role.Type.</summary>
public static class RoleTypes
{
    public const string SystemRole = "System Role";
    public const string CustomRole = "Custom Role";
}
