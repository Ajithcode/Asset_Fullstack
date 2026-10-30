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
        return ValidateSequence(
            request.SequenceName,
            request.SequenceCode,
            request.Module,
            request.RecordType,
            request.Prefix,
            request.Separator,
            request.NumberLength,
            request.StartNumber,
            request.IncrementBy,
            request.ResetFrequency,
            request.Scope,
            request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateNumberingSequenceRequest request)
    {
        return ValidateSequence(
            request.SequenceName,
            request.SequenceCode,
            request.Module,
            request.RecordType,
            request.Prefix,
            request.Separator,
            request.NumberLength,
            request.StartNumber,
            request.IncrementBy,
            request.ResetFrequency,
            request.Scope,
            request.Status);
    }

    // Shared by the create and update requests so both report identical error keys.
    private static Dictionary<string, string[]> ValidateSequence(
        string? sequenceName,
        string? sequenceCode,
        string? module,
        string? recordType,
        string? prefix,
        string? separator,
        int? numberLength,
        long? startNumber,
        int? incrementBy,
        string? resetFrequency,
        string? scope,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "SequenceName", sequenceName);
        Require(errors, "SequenceCode", sequenceCode);
        Require(errors, "Module", module);
        Require(errors, "RecordType", recordType);
        Require(errors, "Prefix", prefix);
        Require(errors, "ResetFrequency", resetFrequency);
        Require(errors, "Scope", scope);
        Require(errors, "Status", status);

        if (!string.IsNullOrWhiteSpace(sequenceCode) && !CodePattern().IsMatch(sequenceCode.Trim()))
        {
            errors["SequenceCode"] = ["SequenceCode may only contain letters, digits, hyphens and underscores"];
        }

        if (!string.IsNullOrWhiteSpace(prefix) && !PrefixPattern().IsMatch(prefix.Trim()))
        {
            errors["Prefix"] = ["Prefix may only contain letters and digits"];
        }

        // Letters or digits in the separator would make the generated numbers ambiguous.
        if (string.IsNullOrEmpty(separator))
        {
            errors["Separator"] = ["Separator is required"];
        }
        else if (separator.Length > MaxSeparatorLength || separator.Any(char.IsLetterOrDigit))
        {
            errors["Separator"] = [$"Separator must be up to {MaxSeparatorLength} non-alphanumeric characters, e.g. \"-\""];
        }

        if (numberLength is null)
        {
            errors["NumberLength"] = ["NumberLength is required"];
        }
        else if (numberLength is < 1 or > MaxNumberLength)
        {
            errors["NumberLength"] = [$"NumberLength must be between 1 and {MaxNumberLength}"];
        }

        if (startNumber is null)
        {
            errors["StartNumber"] = ["StartNumber is required"];
        }
        else if (startNumber < 0)
        {
            errors["StartNumber"] = ["StartNumber must be zero or greater"];
        }
        else if (numberLength is >= 1 and <= MaxNumberLength
            && startNumber.Value.ToString().Length > numberLength)
        {
            errors["StartNumber"] = ["StartNumber has more digits than NumberLength allows"];
        }

        if (incrementBy is null)
        {
            errors["IncrementBy"] = ["IncrementBy is required"];
        }
        else if (incrementBy < 1)
        {
            errors["IncrementBy"] = ["IncrementBy must be 1 or greater"];
        }

        AllowOnly(errors, "ResetFrequency", resetFrequency, AllowedResetFrequencies);
        AllowOnly(errors, "Scope", scope, AllowedScopes);
        AllowOnly(errors, "Status", status, AllowedStatuses);

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
