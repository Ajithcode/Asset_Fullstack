using System.Net.Mail;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class UserValidationExtensions
{
    private static readonly string[] AllowedStatuses = ["Active", "Pending", "Suspended"];

    public static Dictionary<string, string[]> Validate(this CreateUserRequest request)
    {
        var errors = ValidateUser(request.Name, request.Email, request.PrimaryRole, request.AccountStatus);
        Require(errors, nameof(request.Username), request.Username);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateUserRequest request)
    {
        return ValidateUser(request.Name, request.Email, request.PrimaryRole, request.AccountStatus);
    }

    private static Dictionary<string, string[]> ValidateUser(string? name, string? email, string? primaryRole, string? accountStatus)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "Name", name);
        Require(errors, "Email", email);
        Require(errors, "PrimaryRole", primaryRole);
        Require(errors, "AccountStatus", accountStatus);

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email.Trim()))
        {
            errors["Email"] = ["Email is not a valid email address"];
        }

        if (!string.IsNullOrWhiteSpace(accountStatus)
            && !AllowedStatuses.Contains(accountStatus.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors["AccountStatus"] = [$"AccountStatus must be one of: {string.Join(", ", AllowedStatuses)}"];
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        return MailAddress.TryCreate(email, out var address) && address.Address == email;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }
}
