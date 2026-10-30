using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class ChecklistItemValidationExtensions
{
    private static readonly string[] AllowedResponseTypes = ["Pass/Fail", "Yes/No", "Numeric", "Text", "Photo"];

    public static Dictionary<string, string[]> Validate(this CreateChecklistItemRequest request)
    {
        return ValidateFields(request.ItemText, request.Section, request.ResponseType, request.ScoreValue);
    }

    public static Dictionary<string, string[]> Validate(this UpdateChecklistItemRequest request)
    {
        return ValidateFields(request.ItemText, request.Section, request.ResponseType, request.ScoreValue);
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? itemText,
        string? section,
        string? responseType,
        int? scoreValue)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "ItemText", itemText);
        Require(errors, "Section", section);
        Require(errors, "ResponseType", responseType);

        if (!string.IsNullOrWhiteSpace(responseType) && NormalizeResponseType(responseType) is null)
        {
            errors["ResponseType"] = [$"ResponseType must be one of: {string.Join(", ", AllowedResponseTypes)}"];
        }

        if (scoreValue is null)
        {
            errors["ScoreValue"] = ["ScoreValue is required"];
        }
        else if (scoreValue < 0)
        {
            errors["ScoreValue"] = ["ScoreValue must be zero or greater"];
        }

        return errors;
    }

    // Maps "pass / fail", "PassFail", "yes-no", ... onto the canonical response type, or null if unknown.
    public static string? NormalizeResponseType(string? responseType)
    {
        var key = new string((responseType ?? string.Empty).Where(char.IsLetter).ToArray());

        return AllowedResponseTypes.FirstOrDefault(allowed =>
            string.Equals(new string(allowed.Where(char.IsLetter).ToArray()), key, StringComparison.OrdinalIgnoreCase));
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }
}
