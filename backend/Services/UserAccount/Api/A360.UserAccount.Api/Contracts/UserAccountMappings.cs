using A360.UserAccount.Api.Security;
using AssignedPermissionEntity = A360.UserAccount.Domain.Entities.AssignedPermission;
//using AssignedProjectEntity = A360.UserAccount.Domain.Entities.AssignedProject;
//using AreaDetailEntity = A360.UserAccount.Domain.Entities.AreaDetail;
//using BuildingDetailEntity = A360.UserAccount.Domain.Entities.BuildingDetail;
//using CountryDetailEntity = A360.UserAccount.Domain.Entities.CountryDetail;
//using FloorDetailEntity = A360.UserAccount.Domain.Entities.FloorDetail;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;
using UserEntity = A360.UserAccount.Domain.Entities.User;
//using ZoneDetailEntity = A360.UserAccount.Domain.Entities.ZoneDetail;

namespace A360.UserAccount.Api.Contracts;

internal static class UserAccountMappings
{
    public static UserEntity ToEntity(this CreateUserRequest request, PasswordHashingService passwordHashingService)
    {
        return new UserEntity
        {
            UserId = GeneratedIdentifier.Create("U"),
            UserName = Clean(request.UserName),
            ShortName = Clean(request.ShortName),
            ContactNo = Clean(request.ContactNo),
            Email = Clean(request.Email),
            LoginPassword = passwordHashingService.Hash(Clean(request.LoginPassword)),
            ActiveDirectoryUserName = Clean(request.ActiveDirectoryUserName),
            UserRoleId = Clean(request.UserRoleId),
            CreatedBy = Clean(request.CreatedBy),
            CreatedAt = DateTime.UtcNow,
            ClientId = Clean(request.ClientId),
            TenantId = request.TenantId,
            Status = "Active",
            IsDeleted = false,
            LoginStatus = "Active"
        };
    }

    public static void ApplyTo(this UpdateUserRequest request, UserEntity user, PasswordHashingService passwordHashingService)
    {
        user.UserName = Clean(request.UserName);
        user.ShortName = Clean(request.ShortName);
        user.ContactNo = Clean(request.ContactNo);
        user.Email = Clean(request.Email);
        user.LoginPassword = passwordHashingService.Hash(Clean(request.LoginPassword));
        user.ActiveDirectoryUserName = Clean(request.ActiveDirectoryUserName);
        user.UserRoleId = Clean(request.UserRoleId);
        user.UpdatedBy = request.UpdatedBy;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            user.Status = request.Status;
        }
    }

    public static RoleEntity ToEntity(this CreateRoleRequest request)
    {
        return new RoleEntity
        {
            RoleId = GeneratedIdentifier.Create("R"),
            RoleName = Clean(request.RoleName),
            Description = Clean(request.Description),
            //AssignedProjects = ToAssignedProjects(request.AssignedProjects),
            AssignedPermissions = ToAssignedPermissions(request.AssignedPermissions),
            CreatedBy = Clean(request.CreatedBy),
            CreatedAt = DateTime.UtcNow,
            ClientId = Clean(request.ClientId),
            TenantId = request.TenantId,
            Status = "Active",
            IsDeleted = false,
            Action = "Created"
        };
    }

    public static void ApplyTo(this UpdateRoleRequest request, RoleEntity role)
    {
        role.RoleName = Clean(request.RoleName);
        role.Description = Clean(request.Description);
        //role.AssignedProjects = ToAssignedProjects(request.AssignedProjects);
        role.AssignedPermissions = ToAssignedPermissions(request.AssignedPermissions);
        role.UpdatedBy = request.UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            role.Status = request.Status;
        }
        role.Action = "Updated";
    }

    // private static List<AssignedProjectEntity> ToAssignedProjects(
    //     IReadOnlyCollection<AssignedProjectRequest>? assignedProjects)
    // {
    //     return assignedProjects?
    //         .Select(project => new AssignedProjectEntity
    //         {
    //             ProjectId = Clean(project.ProjectId),
    //             ProjectName = Clean(project.ProjectName),
    //             CountryDetails = project.CountryDetails?
    //                 .Select(country => new CountryDetailEntity
    //                 {
    //                     CountryId = Clean(country.CountryId),
    //                     CountryName = Clean(country.CountryName)
    //                 })
    //                 .ToList() ?? [],
    //             AreaDetails = project.AreaDetails?
    //                 .Select(area => new AreaDetailEntity
    //                 {
    //                     AreaId = Clean(area.AreaId),
    //                     AreaName = Clean(area.AreaName)
    //                 })
    //                 .ToList() ?? [],
    //             BuildingDetails = project.BuildingDetails?
    //                 .Select(building => new BuildingDetailEntity
    //                 {
    //                     BuildingId = Clean(building.BuildingId),
    //                     BuildingName = Clean(building.BuildingName)
    //                 })
    //                 .ToList() ?? [],
    //             FloorDetails = project.FloorDetails?
    //                 .Select(floor => new FloorDetailEntity
    //                 {
    //                     FloorId = Clean(floor.FloorId),
    //                     FloorName = Clean(floor.FloorName)
    //                 })
    //                 .ToList() ?? [],
    //             ZoneDetails = project.ZoneDetails?
    //                 .Select(zone => new ZoneDetailEntity
    //                 {
    //                     ZoneId = Clean(zone.ZoneId),
    //                     ZoneName = Clean(zone.ZoneName)
    //                 })
    //                 .ToList() ?? []
    //         })
    //         .ToList() ?? [];
    // }

    private static List<AssignedPermissionEntity> ToAssignedPermissions(
        IReadOnlyCollection<AssignedPermissionRequest>? assignedPermissions)
    {
        return assignedPermissions?
            .Select(permission => new AssignedPermissionEntity
            {
                FeatureName = Clean(permission.FeatureName),
                ViewOption = permission.ViewOption,
                EditOption = permission.EditOption
            })
            .ToList() ?? [];
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
