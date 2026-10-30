using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SystemUserValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSystemUserRequest request)
    {
        return ValidateFields(request.FullName, request.Username, request.Email, request.Department, request.PrimaryRole, request.AdditionalRoles, request.Phone, request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSystemUserRequest request)
    {
        return ValidateFields(request.FullName, request.Username, request.Email, request.Department, request.PrimaryRole, request.AdditionalRoles, request.Phone, request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSystemUserStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? fullName,
        string? username,
        string? email,
        MasterDataReferenceDto? department,
        MasterDataReferenceDto? primaryRole,
        List<MasterDataReferenceDto>? additionalRoles,
        string? phone,
        List<string>? sites)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["FullName"] =
                ["FullName is required"];
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            errors["Username"] =
                ["Username is required"];
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["Email"] =
                ["Email is required"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, null);

        if (string.IsNullOrWhiteSpace(department?.Id))
        {
            errors["Department"] =
                ["Department is required: pick a department by id"];
        }

        if (string.IsNullOrWhiteSpace(primaryRole?.Id))
        {
            errors["PrimaryRole"] =
                ["PrimaryRole is required: pick a role by id"];
        }

        if (additionalRoles is not null)
        {
            if (additionalRoles.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["AdditionalRoles"] =
                    ["Every entry in AdditionalRoles needs a role id"];
            }
            else if (additionalRoles.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["AdditionalRoles"] =
                    ["AdditionalRoles contains duplicate entries"];
            }
            else if (!string.IsNullOrWhiteSpace(primaryRole?.Id)
                && additionalRoles.Any(x => x.Id!.Trim() == primaryRole.Id.Trim()))
            {
                errors["AdditionalRoles"] =
                    ["The primary role can't also be an additional role"];
            }
        }

        ContactValidation.ValidateEmailAndPhone(errors, null, phone, "Phone");

        if (sites is null || !sites.Any(x => !string.IsNullOrWhiteSpace(x)))
        {
            errors["Sites"] =
                ["Give access to at least one site"];
        }

        return errors;
    }
}
