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
            // Taken from the file extension when not sent.
            FileFormat = (Optional(request.FileFormat) ?? DataImportJobValidationExtensions.FormatFromFile(request.SourceFile))?.ToUpperInvariant(),
            DuplicateHandling = Optional(request.DuplicateHandling),
            DateFormat = Optional(request.DateFormat)?.ToUpperInvariant(),
            Delimiter = Optional(request.Delimiter),
            Encoding = Optional(request.Encoding)?.ToUpperInvariant(),
            BatchSize = request.BatchSize ?? DataImportJobValidationExtensions.DefaultBatchSize,
            Schedule = Optional(request.Schedule),
            NotificationRecipients = request.NotificationRecipients?
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Select(recipient => recipient.Trim().ToLowerInvariant())
                .Distinct()
                .ToList() ?? [],
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
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
