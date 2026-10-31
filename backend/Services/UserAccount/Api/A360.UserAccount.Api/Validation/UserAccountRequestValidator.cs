using A360.UserAccount.Api.Contracts;

namespace A360.UserAccount.Api.Validation;

internal static class UserAccountRequestValidator
{
    public static IDictionary<string, string[]> Validate(this CreateUserRequest request)
    {
        var errors = new ValidationErrorBuilder();

        ValidateUserFields(errors, request.UserName, request.ShortName, request.ContactNo, request.Email, request.LoginPassword, request.UserRoleId);
        errors.Required(nameof(request.CreatedBy), request.CreatedBy, "Created by");
        errors.Required(nameof(request.ClientId), request.ClientId, "Client id");

        return errors.ToDictionary();
    }

    public static IDictionary<string, string[]> Validate(this UpdateUserRequest request)
    {
        var errors = new ValidationErrorBuilder();

        ValidateUserFields(errors, request.UserName, request.ShortName, request.ContactNo, request.Email, request.LoginPassword, request.UserRoleId);

        return errors.ToDictionary();
    }

    public static IDictionary<string, string[]> Validate(this CreateRoleRequest request)
    {
        var errors = new ValidationErrorBuilder();

        errors.Required(nameof(request.RoleName), request.RoleName, "Role name");
        errors.Required(nameof(request.CreatedBy), request.CreatedBy, "Created by");
        errors.Required(nameof(request.ClientId), request.ClientId, "Client id");
       // ValidateAssignedProjects(errors, request.AssignedProjects);
        ValidateAssignedPermissions(errors, request.AssignedPermissions);

        return errors.ToDictionary();
    }

    public static IDictionary<string, string[]> Validate(this UpdateRoleRequest request)
    {
        var errors = new ValidationErrorBuilder();

        errors.Required(nameof(request.RoleName), request.RoleName, "Role name");
       // ValidateAssignedProjects(errors, request.AssignedProjects);
        ValidateAssignedPermissions(errors, request.AssignedPermissions);

        return errors.ToDictionary();
    }

    private static void ValidateUserFields(
        ValidationErrorBuilder errors,
        string? userName,
        string? shortName,
        string? contactNo,
        string? email,
        string? loginPassword,
        string? userRoleId)
    {
        errors.Required(nameof(CreateUserRequest.UserName), userName, "User name");
        errors.Required(nameof(CreateUserRequest.ShortName), shortName, "Short name");
        errors.Required(nameof(CreateUserRequest.ContactNo), contactNo, "Contact number");
        errors.Email(nameof(CreateUserRequest.Email), email);
        errors.Required(nameof(CreateUserRequest.LoginPassword), loginPassword, "Login password");
        errors.Required(nameof(CreateUserRequest.UserRoleId), userRoleId, "User role id");
    }

    // private static void ValidateAssignedProjects(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<AssignedProjectRequest>? assignedProjects)
    // {
    //     if (assignedProjects is null)
    //     {
    //         return;
    //     }

    //     var projectIndex = 0;
    //     foreach (var project in assignedProjects)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].ProjectId", project.ProjectId, "Project id");

    //         ValidateCountryDetails(errors, project.CountryDetails, projectIndex);
    //         ValidateAreaDetails(errors, project.AreaDetails, projectIndex);
    //         ValidateBuildingDetails(errors, project.BuildingDetails, projectIndex);
    //         ValidateFloorDetails(errors, project.FloorDetails, projectIndex);
    //         ValidateZoneDetails(errors, project.ZoneDetails, projectIndex);

    //         projectIndex++;
    //     }
    // }

    // private static void ValidateCountryDetails(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<CountryDetailRequest>? details,
    //     int projectIndex)
    // {
    //     if (details is null)
    //     {
    //         return;
    //     }

    //     var detailIndex = 0;
    //     foreach (var detail in details)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].CountryDetails[{detailIndex}].CountryId", detail.CountryId, "Country id");
    //         detailIndex++;
    //     }
    // }

    // private static void ValidateAreaDetails(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<AreaDetailRequest>? details,
    //     int projectIndex)
    // {
    //     if (details is null)
    //     {
    //         return;
    //     }

    //     var detailIndex = 0;
    //     foreach (var detail in details)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].AreaDetails[{detailIndex}].AreaId", detail.AreaId, "Area id");
    //         detailIndex++;
    //     }
    // }

    // private static void ValidateBuildingDetails(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<BuildingDetailRequest>? details,
    //     int projectIndex)
    // {
    //     if (details is null)
    //     {
    //         return;
    //     }

    //     var detailIndex = 0;
    //     foreach (var detail in details)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].BuildingDetails[{detailIndex}].BuildingId", detail.BuildingId, "Building id");
    //         detailIndex++;
    //     }
    // }

    // private static void ValidateFloorDetails(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<FloorDetailRequest>? details,
    //     int projectIndex)
    // {
    //     if (details is null)
    //     {
    //         return;
    //     }

    //     var detailIndex = 0;
    //     foreach (var detail in details)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].FloorDetails[{detailIndex}].FloorId", detail.FloorId, "Floor id");
    //         detailIndex++;
    //     }
    // }

    // private static void ValidateZoneDetails(
    //     ValidationErrorBuilder errors,
    //     IReadOnlyCollection<ZoneDetailRequest>? details,
    //     int projectIndex)
    // {
    //     if (details is null)
    //     {
    //         return;
    //     }

    //     var detailIndex = 0;
    //     foreach (var detail in details)
    //     {
    //         errors.ObjectId($"AssignedProjects[{projectIndex}].ZoneDetails[{detailIndex}].ZoneId", detail.ZoneId, "Zone id");
    //         detailIndex++;
    //     }
    // }

    private static void ValidateAssignedPermissions(
        ValidationErrorBuilder errors,
        IReadOnlyCollection<AssignedPermissionRequest>? assignedPermissions)
    {
        if (assignedPermissions is null)
        {
            return;
        }

        var permissionIndex = 0;
        foreach (var permission in assignedPermissions)
        {
            errors.Required($"AssignedPermissions[{permissionIndex}].FeatureName", permission.FeatureName, "Feature name");
            permissionIndex++;
        }
    }
}
