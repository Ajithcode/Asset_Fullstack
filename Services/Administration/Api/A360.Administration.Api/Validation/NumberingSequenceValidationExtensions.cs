using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class NumberingSequenceValidationExtensions
{
    private const int MaxNumberLength = 18;
    private const int MaxSeparatorLength = 3;

    private static readonly string[] AllowedResetFrequencies = ["Never", "Yearly", "Monthly"];
    private static readonly string[] AllowedScopes = ["Organization", "Location", "Department"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateNumberingSequenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.SequenceName), request.SequenceName);
        Require(errors, nameof(request.SequenceCode), request.SequenceCode);
        Require(errors, nameof(request.Module), request.Module);
        Require(errors, nameof(request.RecordType), request.RecordType);
        Require(errors, nameof(request.Prefix), request.Prefix);
        Require(errors, nameof(request.ResetFrequency), request.ResetFrequency);
        Require(errors, nameof(request.Scope), request.Scope);
        Require(errors, nameof(request.Status), request.Status);

        if (!string.IsNullOrWhiteSpace(request.SequenceCode) && !CodePattern().IsMatch(request.SequenceCode.Trim()))
        {
            errors[nameof(request.SequenceCode)] = ["SequenceCode may only contain letters, digits, hyphens and underscores"];
        }

        if (!string.IsNullOrWhiteSpace(request.Prefix) && !PrefixPattern().IsMatch(request.Prefix.Trim()))
        {
            errors[nameof(request.Prefix)] = ["Prefix may only contain letters and digits"];
        }

        // Letters or digits in the separator would make the generated numbers ambiguous.
        if (string.IsNullOrEmpty(request.Separator))
        {
            errors[nameof(request.Separator)] = ["Separator is required"];
        }
        else if (request.Separator.Length > MaxSeparatorLength || request.Separator.Any(char.IsLetterOrDigit))
        {
            errors[nameof(request.Separator)] = [$"Separator must be up to {MaxSeparatorLength} non-alphanumeric characters, e.g. \"-\""];
        }

        if (request.NumberLength is null)
        {
            errors[nameof(request.NumberLength)] = ["NumberLength is required"];
        }
        else if (request.NumberLength is < 1 or > MaxNumberLength)
        {
            errors[nameof(request.NumberLength)] = [$"NumberLength must be between 1 and {MaxNumberLength}"];
        }

        if (request.StartNumber is null)
        {
            errors[nameof(request.StartNumber)] = ["StartNumber is required"];
        }
        else if (request.StartNumber < 0)
        {
            errors[nameof(request.StartNumber)] = ["StartNumber must be zero or greater"];
        }
        else if (request.NumberLength is >= 1 and <= MaxNumberLength
            && request.StartNumber.Value.ToString().Length > request.NumberLength)
        {
            errors[nameof(request.StartNumber)] = ["StartNumber has more digits than NumberLength allows"];
        }

        if (request.IncrementBy is null)
        {
            errors[nameof(request.IncrementBy)] = ["IncrementBy is required"];
        }
        else if (request.IncrementBy < 1)
        {
            errors[nameof(request.IncrementBy)] = ["IncrementBy must be 1 or greater"];
        }

        AllowOnly(errors, nameof(request.ResetFrequency), request.ResetFrequency, AllowedResetFrequencies);
        AllowOnly(errors, nameof(request.Scope), request.Scope, AllowedScopes);
        AllowOnly(errors, nameof(request.Status), request.Status, AllowedStatuses);

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }

    private static void AllowOnly(Dictionary<string, string[]> errors, string field, string? value, string[] allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex PrefixPattern();
}
