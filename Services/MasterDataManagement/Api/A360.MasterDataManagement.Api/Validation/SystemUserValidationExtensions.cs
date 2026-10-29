using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SystemUserValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSystemUserRequest request)
    {
        return ValidateFields(request.FullName, request.Username, request.Email, request.Department, request.PrimaryRole, request.Phone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSystemUserRequest request)
    {
        return ValidateFields(request.FullName, request.Username, request.Email, request.Department, request.PrimaryRole, request.Phone);
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
        string? phone)
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

        ContactValidation.ValidateEmailAndPhone(errors, null, phone, "Phone");

        return errors;
    }
}
