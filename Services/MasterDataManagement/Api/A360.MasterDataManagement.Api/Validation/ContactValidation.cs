using System.Net.Mail;
using System.Text.RegularExpressions;

namespace A360.MasterDataManagement.Api.Validation;

/// <summary>
/// Email and phone number checks shared by masters that hold contact details.
/// </summary>
internal static class ContactValidation
{
    // An optional leading +, then digits, spaces, dashes and brackets, with 6 to 15 digits in total.
    private static readonly Regex PhoneNumberPattern = new(
        @"^(?=(?:\D*\d){6,15}\D*$)\+?[\d\s\-()]+$",
        RegexOptions.Compiled);

    public static void ValidateEmailAndPhone(
        Dictionary<string, string[]> errors,
        string? email,
        string? phoneNumber,
        string phoneField = "PhoneNumber")
    {
        if (!string.IsNullOrWhiteSpace(email)
            && !(MailAddress.TryCreate(email.Trim(), out var address) && address.Address == email.Trim()))
        {
            errors["Email"] =
                ["Email is not a valid email address"];
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && !PhoneNumberPattern.IsMatch(phoneNumber.Trim()))
        {
            errors[phoneField] =
                [$"{phoneField} may contain only digits, spaces, +, -, ( ) and needs 6 to 15 digits"];
        }
    }
}
