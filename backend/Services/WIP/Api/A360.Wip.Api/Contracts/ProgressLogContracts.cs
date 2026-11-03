
using ProgressLogEntity = A360.Wip.Domain.Entities.ProgressLog;

namespace A360.Wip.Api.Contracts;

public sealed record CreateProgressLogRequest(
    string? LogId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    DateTime? Timestamp,
    double? ProgressPercentage,
    string? UpdateSource,
    string? Remarks,
    string? SensorData,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public ProgressLogEntity ToEntity()
    {
        return new ProgressLogEntity
        {
            LogId = LogId,
            AssetId = AssetId,
            AssetName = AssetName,
            JobId = JobId,
            TaskId = TaskId,

            Timestamp = Timestamp,
            ProgressPercentage = ProgressPercentage,
            UpdateSource = UpdateSource,
            Remarks = Remarks,
            SensorData = SensorData,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateProgressLogRequest(
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    DateTime? Timestamp,
    double? ProgressPercentage,
    string? UpdateSource,
    string? Remarks,
    string? SensorData,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(ProgressLogEntity progressLog)
    {
        progressLog.AssetId = AssetId;
        progressLog.AssetName = AssetName;
        progressLog.JobId = JobId;
        progressLog.TaskId = TaskId;

        progressLog.Timestamp = Timestamp;
        progressLog.ProgressPercentage = ProgressPercentage;
        progressLog.UpdateSource = UpdateSource;
        progressLog.Remarks = Remarks;
        progressLog.SensorData = SensorData;

        progressLog.UpdatedBy = UpdatedBy;
        progressLog.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            progressLog.Status = Status;
        }
    }
}

public sealed record ProgressLogResponse(
    string Id,
    string LogId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    DateTime? Timestamp,
    double? ProgressPercentage,
    string? UpdateSource,
    string? Remarks,
    string? SensorData,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ProgressLogResponse FromEntity(ProgressLogEntity progressLog)
    {
        return new ProgressLogResponse(
            progressLog.Id ?? string.Empty,
            progressLog.LogId ?? string.Empty,
            progressLog.AssetId,
            progressLog.AssetName,
            progressLog.JobId,
            progressLog.TaskId,

            progressLog.Timestamp,
            progressLog.ProgressPercentage,
            progressLog.UpdateSource,
            progressLog.Remarks,
            progressLog.SensorData,

            progressLog.CreatedBy,
            progressLog.CreatedAt,
            progressLog.UpdatedBy,
            progressLog.UpdatedAt,

            progressLog.ClientId,
            progressLog.TenantId,
            progressLog.Status,
            progressLog.IsDeleted
        );
    }
}
