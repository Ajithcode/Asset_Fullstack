using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateDataImportJobRequest(
    string? JobName,
    string? DataObject,
    string? Operation,
    string? SourceFile,
    string? FileFormat,
    string? DuplicateHandling,
    string? DateFormat,
    string? Delimiter,
    string? Encoding,
    int? BatchSize,
    string? Schedule,
    List<string>? NotificationRecipients,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

// JobId, TenantId and ClientId are fixed at creation.
public sealed record UpdateDataImportJobRequest(
    string? JobName,
    string? DataObject,
    string? Operation,
    string? SourceFile,
    string? FileFormat,
    string? DuplicateHandling,
    string? DateFormat,
    string? Delimiter,
    string? Encoding,
    int? BatchSize,
    string? Schedule,
    List<string>? NotificationRecipients,
    string? Status,
    string? UpdatedBy);

public sealed record DataImportJobResponse(
    string JobId,
    string JobName,
    string DataObject,
    string Operation,
    string SourceFile,
    string? FileFormat,
    string? DuplicateHandling,
    string? DateFormat,
    string? Delimiter,
    string? Encoding,
    int BatchSize,
    string? Schedule,
    IReadOnlyCollection<string> NotificationRecipients,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static DataImportJobResponse FromEntity(DataImportJob job)
    {
        return new DataImportJobResponse(
            job.JobId,
            job.JobName,
            job.DataObject,
            job.Operation,
            job.SourceFile,
            job.FileFormat,
            job.DuplicateHandling,
            job.DateFormat,
            job.Delimiter,
            job.Encoding,
            job.BatchSize,
            job.Schedule,
            job.NotificationRecipients,
            job.Status,
            job.CreatedBy,
            job.CreatedAt,
            job.UpdatedBy,
            job.UpdatedAt,
            job.TenantId,
            job.ClientId);
    }
}
