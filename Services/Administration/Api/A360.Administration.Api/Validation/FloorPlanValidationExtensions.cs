using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Validation;

public static partial class FloorPlanValidationExtensions
{
    private const int NameMaxLength = 150;
    private const int FileNameMaxLength = 255;
    private const int DescriptionMaxLength = 1000;

    public static Dictionary<string, string[]> Validate(this CreateFloorPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.Name), request.Name, NameMaxLength);
        Require(errors, nameof(request.LocationPath), request.LocationPath, NameMaxLength * 3);
        Require(errors, nameof(request.Type), request.Type, NameMaxLength);
        Require(errors, nameof(request.FileName), request.FileName, FileNameMaxLength);

        OneOf(errors, nameof(request.Type), request.Type, FloorPlanType.All);
        OneOf(errors, nameof(request.Status), request.Status, FloorPlanStatus.All);

        if (!errors.ContainsKey(nameof(request.FileName)) && FileFormatOf(request.FileFormat, request.FileName) is null)
        {
            errors[nameof(request.FileFormat)] = [$"File format must be one of: {string.Join(", ", FloorPlanFileFormat.All)}"];
        }

        if (request.FileSizeBytes is < 0)
        {
            errors[nameof(request.FileSizeBytes)] = ["FileSizeBytes must be zero or greater"];
        }

        if (!string.IsNullOrWhiteSpace(request.Version) && NormalizeVersion(request.Version) is null)
        {
            errors[nameof(request.Version)] = ["Version must look like v1, v2.1 or 2.1.3"];
        }

        Positive(errors, nameof(request.Width), request.Width);
        Positive(errors, nameof(request.Length), request.Length);

        if (request.Scale is <= 0)
        {
            errors[nameof(request.Scale)] = ["Scale must be greater than zero (e.g. 100 for 1 : 100)"];
        }

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors[nameof(request.Description)] = [$"Description must be at most {DescriptionMaxLength} characters"];
        }

        if (request.PublishedOn?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors[nameof(request.PublishedOn)] = ["PublishedOn cannot be in the future"];
        }

        var published = string.Equals(request.Status?.Trim(), FloorPlanStatus.Published, StringComparison.OrdinalIgnoreCase);
        if (!published && (request.PublishedOn is not null || !string.IsNullOrWhiteSpace(request.PublishedBy)))
        {
            errors[nameof(request.Status)] = ["PublishedOn and PublishedBy are only allowed when Status is Published"];
        }

        return errors;
    }

    /// <summary>The allowed value matching ignoring case, or null when missing or not allowed.</summary>
    public static string? Normalize(string? value, IReadOnlyList<string> allowed)
    {
        return allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The given format, or the file name's extension, as an allowed upper-case format; null when not allowed.</summary>
    public static string? FileFormatOf(string? fileFormat, string? fileName)
    {
        var format = string.IsNullOrWhiteSpace(fileFormat)
            ? Path.GetExtension(fileName?.Trim() ?? string.Empty).TrimStart('.')
            : fileFormat.Trim().TrimStart('.');

        return Normalize(format, FloorPlanFileFormat.All);
    }

    /// <summary>"2.1", "V2.1" and "v2.1" all become "v2.1"; null when not a version.</summary>
    public static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var match = VersionPattern().Match(version.Trim());
        return match.Success ? $"v{match.Groups[1].Value}" : null;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} must be at most {maxLength} characters"];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && Normalize(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void Positive(Dictionary<string, string[]> errors, string field, double? value)
    {
        if (value is not null && (value <= 0 || !double.IsFinite(value.Value)))
        {
            errors[field] = [$"{field} must be greater than zero"];
        }
    }

    [GeneratedRegex(@"^[vV]?(\d+(\.\d+){0,2})$")]
    private static partial Regex VersionPattern();
}
