using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class DataImportJobMappings
{
    public static DataImportJob ToEntity(this CreateDataImportJobRequest request, CurrentUser? currentUser)
    {
        return new DataImportJob
        {
            // Generated on the server so every job gets a stable id independent of the Mongo _id.
            JobId = Guid.NewGuid().ToString(),
            JobName = request.JobName!.Trim(),
            DataObject = request.DataObject!.Trim(),
            Operation = request.Operation!.Trim(),
            SourceFile = request.SourceFile!.Trim(),
            FileFormat = FileFormat(request.FileFormat, request.SourceFile),
            DuplicateHandling = Optional(request.DuplicateHandling),
            DateFormat = Optional(request.DateFormat)?.ToUpperInvariant(),
            Delimiter = Optional(request.Delimiter),
            Encoding = Optional(request.Encoding)?.ToUpperInvariant(),
            BatchSize = request.BatchSize ?? DataImportJobValidationExtensions.DefaultBatchSize,
            Schedule = Optional(request.Schedule),
            NotificationRecipients = Recipients(request.NotificationRecipients),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    public static void ApplyTo(this UpdateDataImportJobRequest request, DataImportJob job, CurrentUser? currentUser)
    {
        job.JobName = request.JobName!.Trim();
        job.DataObject = request.DataObject!.Trim();
        job.Operation = request.Operation!.Trim();
        job.SourceFile = request.SourceFile!.Trim();
        job.FileFormat = FileFormat(request.FileFormat, request.SourceFile);
        job.DuplicateHandling = Optional(request.DuplicateHandling);
        job.DateFormat = Optional(request.DateFormat)?.ToUpperInvariant();
        job.Delimiter = Optional(request.Delimiter);
        job.Encoding = Optional(request.Encoding)?.ToUpperInvariant();
        job.BatchSize = request.BatchSize ?? DataImportJobValidationExtensions.DefaultBatchSize;
        job.Schedule = Optional(request.Schedule);
        job.NotificationRecipients = Recipients(request.NotificationRecipients);
        job.Status = request.Status!.Trim();
        job.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        job.UpdatedAt = DateTime.UtcNow;
    }

    public static void MarkDeleted(this DataImportJob job, CurrentUser? currentUser)
    {
        job.IsDeleted = true;
        job.UpdatedBy = currentUser?.UserName ?? job.UpdatedBy;
        job.UpdatedAt = DateTime.UtcNow;
    }

    // Taken from the file extension when not sent.
    private static string? FileFormat(string? fileFormat, string? sourceFile)
    {
        return (Optional(fileFormat) ?? DataImportJobValidationExtensions.FormatFromFile(sourceFile))?.ToUpperInvariant();
    }

    private static List<string> Recipients(List<string>? recipients)
    {
        return recipients?
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
            .Select(recipient => recipient.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? [];
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
