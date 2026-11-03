using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;
using ModuleAccessEntity = A360.MasterDataManagement.Domain.Entities.ModuleAccess;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Contracts;

/// <summary>
/// One row of the permission matrix. Only ModuleId and the six flags are needed on requests;
/// the module name is filled from the module master.
/// </summary>
public sealed record RoleModulePermissionDto(
    string? ModuleId,
    string? ModuleName,
    bool View,
    bool Create,
    bool Edit,
    bool Delete,
    bool Approve,
    bool Export)
{
    public RoleModulePermission ToValue()
    {
        return new RoleModulePermission
        {
            ModuleId = ModuleId!.Trim(),
            ModuleName = ModuleName?.Trim() ?? string.Empty,
            View = View,
            Create = Create,
            Edit = Edit,
            Delete = Delete,
            Approve = Approve,
            Export = Export
        };
    }
}

// CopyFromRoleId fills Permissions, ModuleAccess and ModulePermissions from an existing role when those lists are left out.
public sealed record CreateRoleRequest(
    string? RoleName,
    string? Code,
    string? Type,
    string? Description,
    List<MasterDataReferenceDto>? Permissions,
    List<MasterDataReferenceDto>? ModuleAccess,
    List<RoleModulePermissionDto>? ModulePermissions,
    string? DataScope,
    List<MasterDataReferenceDto>? Locations,
    List<MasterDataReferenceDto>? Departments,
    decimal? ApprovalLimit,
    string? ApprovalCurrency,
    bool? MfaRequired,
    string? CopyFromRoleId,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public RoleEntity ToEntity()
    {
        return new RoleEntity
        {
            RoleName = RoleName!.Trim(),
            Code = Code!.Trim(),
            Type = RoleContractDefaults.Type(Type),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Permissions = Permissions?.Select(x => x.ToReference()).ToList() ?? [],
            ModuleAccess = ModuleAccess?.Select(x => x.ToReference()).ToList() ?? [],
            ModulePermissions = ModulePermissions?.Select(x => x.ToValue()).ToList() ?? [],
            DataScope = RoleContractDefaults.DataScope(DataScope),
            Locations = Locations?.Select(x => x.ToReference()).ToList() ?? [],
            Departments = Departments?.Select(x => x.ToReference()).ToList() ?? [],
            ApprovalLimit = ApprovalLimit ?? 0,
            ApprovalCurrency = RoleContractDefaults.Currency(ApprovalCurrency),
            MfaRequired = MfaRequired ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateRoleRequest(
    string? RoleName,
    string? Code,
    string? Type,
    string? Description,
    List<MasterDataReferenceDto>? Permissions,
    List<MasterDataReferenceDto>? ModuleAccess,
    List<RoleModulePermissionDto>? ModulePermissions,
    string? DataScope,
    List<MasterDataReferenceDto>? Locations,
    List<MasterDataReferenceDto>? Departments,
    decimal? ApprovalLimit,
    string? ApprovalCurrency,
    bool? MfaRequired,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(RoleEntity role)
    {
        role.RoleName = RoleName!.Trim();
        role.Code = Code!.Trim();
        role.Type = RoleContractDefaults.Type(Type);
        role.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        role.Permissions = Permissions?.Select(x => x.ToReference()).ToList() ?? [];

        // A request with ModulePermissions drives ModuleAccess; otherwise ModuleAccess is taken as sent.
        if (ModulePermissions is not null)
        {
            role.ModulePermissions = ModulePermissions.Select(x => x.ToValue()).ToList();
        }
        else
        {
            role.ModuleAccess = ModuleAccess?.Select(x => x.ToReference()).ToList() ?? [];
        }

        role.DataScope = RoleContractDefaults.DataScope(DataScope);
        role.Locations = Locations?.Select(x => x.ToReference()).ToList() ?? [];
        role.Departments = Departments?.Select(x => x.ToReference()).ToList() ?? [];
        role.ApprovalLimit = ApprovalLimit ?? 0;
        role.ApprovalCurrency = RoleContractDefaults.Currency(ApprovalCurrency);

        if (MfaRequired is not null)
        {
            role.MfaRequired = MfaRequired.Value;
        }

        role.UpdatedBy = UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            role.Status = Status;
        }
    }
}

public sealed record UpdateRoleStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record UpdateRolePermissionMatrixRequest(
    List<RoleModulePermissionDto>? ModulePermissions,
    string? UpdatedBy);

public sealed record DuplicateRoleRequest(
    string? RoleName,
    string? Code,
    string? CreatedBy);

public sealed record ImportRolesRequest(
    List<CreateRoleRequest>? Roles,
    bool UpdateExisting,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record ImportRoleResult(
    int Row,
    string? Code,
    string Outcome,
    string? RoleId,
    string? Message);

public sealed record ImportRolesResponse(
    int Created,
    int Updated,
    int Failed,
    IReadOnlyList<ImportRoleResult> Results);

public sealed record RolePermissionMatrixRow(
    string ModuleId,
    string ModuleName,
    string Code,
    string? Icon,
    int SortOrder,
    IReadOnlyList<string> AvailableActions,
    bool View,
    bool Create,
    bool Edit,
    bool Delete,
    bool Approve,
    bool Export,
    string AccessLevel);

public sealed record RolePermissionMatrixResponse(
    string? RoleId,
    string? RoleName,
    IReadOnlyList<RolePermissionMatrixRow> Modules);

public sealed record RoleSummaryResponse(
    int TotalRoles,
    int CreatedThisMonth,
    int ActiveRoles,
    double ActivePercent,
    int CustomRoles,
    double CustomPercent,
    int SystemRoles,
    int UsersAssigned,
    int UsersAssignedLastMonth,
    double UsersAssignedChangePercent);

public sealed record RoleOptionsResponse(
    IReadOnlyList<string> RoleTypes,
    IReadOnlyList<string> DataScopes,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> AccessLevels);

public sealed record RoleUserResponse(
    string Id,
    string FullName,
    string Email,
    MasterDataReferenceDto? Department,
    string? Status);

public sealed record RoleModulePermissionResponse(
    string ModuleId,
    string ModuleName,
    bool View,
    bool Create,
    bool Edit,
    bool Delete,
    bool Approve,
    bool Export);

public sealed record RoleResponse(
    string Id,
    string RoleName,
    string Code,
    string Type,
    string? Description,
    IReadOnlyList<MasterDataReferenceDto> Permissions,
    IReadOnlyList<MasterDataReferenceDto> ModuleAccess,
    IReadOnlyList<RoleModulePermissionResponse> ModulePermissions,
    string DataScope,
    IReadOnlyList<MasterDataReferenceDto> Locations,
    IReadOnlyList<MasterDataReferenceDto> Departments,
    decimal ApprovalLimit,
    string ApprovalCurrency,
    bool MfaRequired,
    int UserCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static RoleResponse FromEntity(
        RoleEntity role,
        int userCount)
    {
        return new RoleResponse(
            role.Id ?? string.Empty,
            role.RoleName ?? string.Empty,
            role.Code ?? string.Empty,
            role.Type ?? string.Empty,
            role.Description,
            (role.Permissions ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (role.ModuleAccess ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (role.ModulePermissions ?? [])
                .Select(x => new RoleModulePermissionResponse(
                    x.ModuleId, x.ModuleName, x.View, x.Create, x.Edit, x.Delete, x.Approve, x.Export))
                .ToList(),
            string.IsNullOrWhiteSpace(role.DataScope) ? RoleDataScopes.AllLocations : role.DataScope,
            (role.Locations ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (role.Departments ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            role.ApprovalLimit,
            string.IsNullOrWhiteSpace(role.ApprovalCurrency) ? RoleContractDefaults.DefaultCurrency : role.ApprovalCurrency,
            role.MfaRequired,
            userCount,
            role.CreatedBy,
            role.CreatedAt,
            role.UpdatedBy,
            role.UpdatedAt,
            role.ClientId,
            role.TenantId,
            role.Status,
            role.IsDeleted
        );
    }
}

public static class RoleContractDefaults
{
    public const string DefaultCurrency = "AED";

    public const string FullAccess = "Full";
    public const string PartialAccess = "Partial";
    public const string NoAccess = "None";

    public static string Type(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? RoleTypes.CustomRole : type.Trim();
    }

    /// <summary>Returns the canonical spelling of a known data scope, the default when blank, or the value as typed.</summary>
    public static string DataScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return RoleDataScopes.AllLocations;
        }

        return RoleDataScopes.All.FirstOrDefault(x => string.Equals(x, scope.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? scope.Trim();
    }

    public static string Currency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
    }

    public static bool IsSystem(string? type)
    {
        return string.Equals(type?.Trim(), RoleTypes.SystemRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type?.Trim(), "System", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds one matrix row for a module, using the role's flags when it has a row for that module.</summary>
    public static RolePermissionMatrixRow ToMatrixRow(
        ModuleAccessEntity module,
        RoleModulePermission? granted)
    {
        var actions = ModuleActionList.Normalize(module.AvailableActions);
        bool Has(string action, bool flag) => flag && actions.Contains(action);

        var view = Has(ModuleActions.View, granted?.View ?? false);
        var create = Has(ModuleActions.Create, granted?.Create ?? false);
        var edit = Has(ModuleActions.Edit, granted?.Edit ?? false);
        var delete = Has(ModuleActions.Delete, granted?.Delete ?? false);
        var approve = Has(ModuleActions.Approve, granted?.Approve ?? false);
        var export = Has(ModuleActions.Export, granted?.Export ?? false);

        var grantedCount = new[] { view, create, edit, delete, approve, export }.Count(x => x);
        var level = grantedCount == 0
            ? NoAccess
            : grantedCount == actions.Count ? FullAccess : PartialAccess;

        return new RolePermissionMatrixRow(
            module.Id,
            module.ModuleName,
            module.Code,
            module.Icon,
            module.SortOrder,
            actions,
            view, create, edit, delete, approve, export,
            level);
    }
}
