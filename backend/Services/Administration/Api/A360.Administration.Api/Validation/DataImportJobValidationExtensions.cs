using System.Net.Mail;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class DataImportJobValidationExtensions
{
    public const int DefaultBatchSize = 1000;
    private const int MaxBatchSize = 100_000;

    private static readonly string[] AllowedOperations = ["Insert", "Update", "Upsert"];
    private static readonly string[] AllowedFileFormats = ["XLSX", "CSV"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateDataImportJobRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.JobName), request.JobName);
        Require(errors, nameof(request.DataObject), request.DataObject);
        Require(errors, nameof(request.Operation), request.Operation);
        Require(errors, nameof(request.SourceFile), request.SourceFile);
        Require(errors, nameof(request.Status), request.Status);

        AllowOnly(errors, nameof(request.Operation), request.Operation, AllowedOperations);
        AllowOnly(errors, nameof(request.Status), request.Status, AllowedStatuses);

        var fileFormat = string.IsNullOrWhiteSpace(request.FileFormat) ? null : request.FileFormat.Trim();
        var fileExtensionFormat = FormatFromFile(request.SourceFile);
        if (fileFormat is not null)
        {
            AllowOnly(errors, nameof(request.FileFormat), fileFormat, AllowedFileFormats);
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceFile) && fileExtensionFormat is null)
        {
            errors[nameof(request.SourceFile)] = ["SourceFile must be an .xlsx or .csv file"];
        }

        if (fileFormat is not null && fileExtensionFormat is not null
            && !string.Equals(fileFormat, fileExtensionFormat, StringComparison.OrdinalIgnoreCase))
        {
            errors[nameof(request.FileFormat)] = [$"FileFormat {fileFormat} does not match the source file extension"];
        }

        if (request.BatchSize is < 1 or > MaxBatchSize)
        {
            errors[nameof(request.BatchSize)] = [$"BatchSize must be between 1 and {MaxBatchSize}"];
        }

        var invalidRecipients = request.NotificationRecipients?
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient) && !IsValidEmail(recipient.Trim()))
            .ToList() ?? [];
        if (invalidRecipients.Count > 0)
        {
            errors[nameof(request.NotificationRecipients)] = [$"Invalid email addresses: {string.Join(", ", invalidRecipients)}"];
        }

        return errors;
    }

    // Returns XLSX or CSV based on the file extension, or null if it is neither.
    public static string? FormatFromFile(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return null;
        }

        var path = Uri.TryCreate(sourceFile.Trim(), UriKind.Absolute, out var uri) ? uri.AbsolutePath : sourceFile.Trim();
        var extension = Path.GetExtension(path).TrimStart('.');

        return AllowedFileFormats.FirstOrDefault(format => string.Equals(format, extension, StringComparison.OrdinalIgnoreCase));
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

    private static void AllowOnly(Dictionary<string, string[]> errors, string field, string? value, string[] allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }
}
