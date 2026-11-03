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
        return ValidateJob(
            request.JobName,
            request.DataObject,
            request.Operation,
            request.SourceFile,
            request.FileFormat,
            request.BatchSize,
            request.NotificationRecipients,
            request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateDataImportJobRequest request)
    {
        return ValidateJob(
            request.JobName,
            request.DataObject,
            request.Operation,
            request.SourceFile,
            request.FileFormat,
            request.BatchSize,
            request.NotificationRecipients,
            request.Status);
    }

    private static Dictionary<string, string[]> ValidateJob(
        string? jobName,
        string? dataObject,
        string? operation,
        string? sourceFile,
        string? requestedFileFormat,
        int? batchSize,
        List<string>? notificationRecipients,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "JobName", jobName);
        Require(errors, "DataObject", dataObject);
        Require(errors, "Operation", operation);
        Require(errors, "SourceFile", sourceFile);
        Require(errors, "Status", status);

        AllowOnly(errors, "Operation", operation, AllowedOperations);
        AllowOnly(errors, "Status", status, AllowedStatuses);

        var fileFormat = string.IsNullOrWhiteSpace(requestedFileFormat) ? null : requestedFileFormat.Trim();
        var fileExtensionFormat = FormatFromFile(sourceFile);
        if (fileFormat is not null)
        {
            AllowOnly(errors, "FileFormat", fileFormat, AllowedFileFormats);
        }
        else if (!string.IsNullOrWhiteSpace(sourceFile) && fileExtensionFormat is null)
        {
            errors["SourceFile"] = ["SourceFile must be an .xlsx or .csv file"];
        }

        if (fileFormat is not null && fileExtensionFormat is not null
            && !string.Equals(fileFormat, fileExtensionFormat, StringComparison.OrdinalIgnoreCase))
        {
            errors["FileFormat"] = [$"FileFormat {fileFormat} does not match the source file extension"];
        }

        if (batchSize is < 1 or > MaxBatchSize)
        {
            errors["BatchSize"] = [$"BatchSize must be between 1 and {MaxBatchSize}"];
        }

        var invalidRecipients = notificationRecipients?
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient) && !IsValidEmail(recipient.Trim()))
            .ToList() ?? [];
        if (invalidRecipients.Count > 0)
        {
            errors["NotificationRecipients"] = [$"Invalid email addresses: {string.Join(", ", invalidRecipients)}"];
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
