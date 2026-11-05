using System.Net.Mail;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class UserValidationExtensions
{
    private static readonly string[] AllowedStatuses = ["Active", "Pending", "Suspended"];

    public const int MinPasswordLength = 8;

    public static Dictionary<string, string[]> Validate(this CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateFields(errors, request.Name, request.Username, request.Email, request.PrimaryRole, request.AccountStatus);

        // Optional: invited / imported users get no sign-in account until a password is set
        ValidatePassword(errors, request.Password);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateFields(errors, request.Name, request.Username, request.Email, request.PrimaryRole, request.AccountStatus);

        // Optional on update: blank keeps the current password
        ValidatePassword(errors, request.Password);

        return errors;
    }

    private static void ValidateFields(
        Dictionary<string, string[]> errors,
        string? name,
        string? username,
        string? email,
        string? primaryRole,
        string? accountStatus)
    {
        Require(errors, "Name", name);
        Require(errors, "Username", username);
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
    }

    private static void ValidatePassword(Dictionary<string, string[]> errors, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        if (password.Length < MinPasswordLength || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors["Password"] = [$"Password must be at least {MinPasswordLength} characters and contain letters and numbers"];
        }
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
