using System.Net.Mail;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class UserValidationExtensions
{
    private static readonly string[] AllowedStatuses = ["Active", "Pending", "Suspended"];

    public static Dictionary<string, string[]> Validate(this CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.Name), request.Name);
        Require(errors, nameof(request.Username), request.Username);
        Require(errors, nameof(request.Email), request.Email);
        Require(errors, nameof(request.PrimaryRole), request.PrimaryRole);
        Require(errors, nameof(request.AccountStatus), request.AccountStatus);

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email.Trim()))
        {
            errors[nameof(request.Email)] = ["Email is not a valid email address"];
        }

        if (!string.IsNullOrWhiteSpace(request.AccountStatus)
            && !AllowedStatuses.Contains(request.AccountStatus.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[nameof(request.AccountStatus)] = [$"AccountStatus must be one of: {string.Join(", ", AllowedStatuses)}"];
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
