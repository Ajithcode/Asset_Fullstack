using System.Text.Json.Serialization;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;

namespace A360.UserAccount.Api.Contracts;

public sealed record CreateRoleRequest(
    string? RoleName,
    string? Description,
   // [property: JsonPropertyName("assignedProject")]
    //IReadOnlyCollection<AssignedProjectRequest>? AssignedProjects,
    IReadOnlyCollection<AssignedPermissionRequest>? AssignedPermissions,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record UpdateRoleRequest(
    string? RoleName,
    string? Description,
   // [property: JsonPropertyName("assignedProject")]
   // IReadOnlyCollection<AssignedProjectRequest>? AssignedProjects,
    IReadOnlyCollection<AssignedPermissionRequest>? AssignedPermissions,
    string? UpdatedBy,
    string? Status);

// public sealed record AssignedProjectRequest(
//     string? ProjectId,
//     string? ProjectName,
//     IReadOnlyCollection<CountryDetailRequest>? CountryDetails,
//     IReadOnlyCollection<AreaDetailRequest>? AreaDetails,
//     IReadOnlyCollection<BuildingDetailRequest>? BuildingDetails,
//     IReadOnlyCollection<FloorDetailRequest>? FloorDetails,
//     IReadOnlyCollection<ZoneDetailRequest>? ZoneDetails);

// public sealed record CountryDetailRequest(string? CountryId, string? CountryName);

// public sealed record AreaDetailRequest(string? AreaId, string? AreaName);

// public sealed record BuildingDetailRequest(string? BuildingId, string? BuildingName);

// public sealed record FloorDetailRequest(string? FloorId, string? FloorName);

// public sealed record ZoneDetailRequest(string? ZoneId, string? ZoneName);

public sealed record AssignedPermissionRequest(
    string? FeatureName,
    bool ViewOption,
    bool EditOption);

public sealed record RoleResponse(
    string Id,
    string RoleId,
    string RoleName,
    string Description,
    //IReadOnlyCollection<AssignedProjectResponse> AssignedProjects,
    IReadOnlyCollection<AssignedPermissionResponse> AssignedPermissions,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted,
    string Action)
{
    public static RoleResponse FromEntity(RoleEntity role)
    {
        return new RoleResponse(
            role.Id,
            role.RoleId,
            role.RoleName,
            role.Description,
            //role.AssignedProjects.Select(AssignedProjectResponse.FromEntity).ToArray(),
            role.AssignedPermissions.Select(AssignedPermissionResponse.FromEntity).ToArray(),
            role.CreatedBy,
            role.CreatedAt,
            role.UpdatedBy,
            role.UpdatedAt,
            role.ClientId,
            role.TenantId,
            role.Status,
            role.IsDeleted,
            role.Action);
    }
}

// public sealed record AssignedProjectResponse(
//     string ProjectId,
//     string ProjectName,
//     IReadOnlyCollection<CountryDetailResponse> CountryDetails,
//     IReadOnlyCollection<AreaDetailResponse> AreaDetails,
//     IReadOnlyCollection<BuildingDetailResponse> BuildingDetails,
//     IReadOnlyCollection<FloorDetailResponse> FloorDetails,
//     IReadOnlyCollection<ZoneDetailResponse> ZoneDetails)
// {
//     public static AssignedProjectResponse FromEntity(A360.UserAccount.Domain.Entities.AssignedProject project)
//     {
//         return new AssignedProjectResponse(
//             project.ProjectId,
//             project.ProjectName,
//             project.CountryDetails.Select(CountryDetailResponse.FromEntity).ToArray(),
//             project.AreaDetails.Select(AreaDetailResponse.FromEntity).ToArray(),
//             project.BuildingDetails.Select(BuildingDetailResponse.FromEntity).ToArray(),
//             project.FloorDetails.Select(FloorDetailResponse.FromEntity).ToArray(),
//             project.ZoneDetails.Select(ZoneDetailResponse.FromEntity).ToArray());
//     }
// }

// public sealed record CountryDetailResponse(string CountryId, string CountryName)
// {
//     public static CountryDetailResponse FromEntity(A360.UserAccount.Domain.Entities.CountryDetail detail)
//     {
//         return new CountryDetailResponse(detail.CountryId, detail.CountryName);
//     }
// }

// public sealed record AreaDetailResponse(string AreaId, string AreaName)
// {
//     public static AreaDetailResponse FromEntity(A360.UserAccount.Domain.Entities.AreaDetail detail)
//     {
//         return new AreaDetailResponse(detail.AreaId, detail.AreaName);
//     }
// }

// public sealed record BuildingDetailResponse(string BuildingId, string BuildingName)
// {
//     public static BuildingDetailResponse FromEntity(A360.UserAccount.Domain.Entities.BuildingDetail detail)
//     {
//         return new BuildingDetailResponse(detail.BuildingId, detail.BuildingName);
//     }
// }

// public sealed record FloorDetailResponse(string FloorId, string FloorName)
// {
//     public static FloorDetailResponse FromEntity(A360.UserAccount.Domain.Entities.FloorDetail detail)
//     {
//         return new FloorDetailResponse(detail.FloorId, detail.FloorName);
//     }
// }

// public sealed record ZoneDetailResponse(string ZoneId, string ZoneName)
// {
//     public static ZoneDetailResponse FromEntity(A360.UserAccount.Domain.Entities.ZoneDetail detail)
//     {
//         return new ZoneDetailResponse(detail.ZoneId, detail.ZoneName);
//     }
// }

public sealed record AssignedPermissionResponse(
    string FeatureName,
    bool ViewOption,
    bool EditOption)
{
    public static AssignedPermissionResponse FromEntity(A360.UserAccount.Domain.Entities.AssignedPermission permission)
    {
        return new AssignedPermissionResponse(
            permission.FeatureName,
            permission.ViewOption,
            permission.EditOption);
    }
}
