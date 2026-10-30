using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class ChecklistItemValidationExtensions
{
    private static readonly string[] AllowedResponseTypes = ["Pass/Fail", "Yes/No", "Numeric", "Text", "Photo"];

    public static Dictionary<string, string[]> Validate(this CreateChecklistItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.ItemText), request.ItemText);
        Require(errors, nameof(request.Section), request.Section);
        Require(errors, nameof(request.ResponseType), request.ResponseType);

        if (!string.IsNullOrWhiteSpace(request.ResponseType) && NormalizeResponseType(request.ResponseType) is null)
        {
            errors[nameof(request.ResponseType)] = [$"ResponseType must be one of: {string.Join(", ", AllowedResponseTypes)}"];
        }

        if (request.ScoreValue is null)
        {
            errors[nameof(request.ScoreValue)] = ["ScoreValue is required"];
        }
        else if (request.ScoreValue < 0)
        {
            errors[nameof(request.ScoreValue)] = ["ScoreValue must be zero or greater"];
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
