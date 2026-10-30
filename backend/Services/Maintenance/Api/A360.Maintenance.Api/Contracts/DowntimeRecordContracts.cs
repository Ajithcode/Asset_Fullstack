
using DowntimeRecordEntity = A360.Maintenance.Domain.Entities.DowntimeRecord;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateDowntimeRecordRequest(
    string? AssetId,
    string? AssetName,
    DateTime? DowntimeStart,
    DateTime? DowntimeEnd,
    string? TotalDowntime,
    string? ReasonForDowntime,
    string? ImpactLevel,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public DowntimeRecordEntity ToEntity()
    {
        return new DowntimeRecordEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,
            DowntimeStart = DowntimeStart,
            DowntimeEnd = DowntimeEnd,
            TotalDowntime = TotalDowntime,
            ReasonForDowntime = ReasonForDowntime,
            ImpactLevel = ImpactLevel,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateDowntimeRecordRequest(
    string? AssetId,
    string? AssetName,
    DateTime? DowntimeStart,
    DateTime? DowntimeEnd,
    string? TotalDowntime,
    string? ReasonForDowntime,
    string? ImpactLevel,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(DowntimeRecordEntity downtimeRecord)
    {
        downtimeRecord.AssetId = AssetId;
        downtimeRecord.AssetName = AssetName;
        downtimeRecord.DowntimeStart = DowntimeStart;
        downtimeRecord.DowntimeEnd = DowntimeEnd;
        downtimeRecord.TotalDowntime = TotalDowntime;
        downtimeRecord.ReasonForDowntime = ReasonForDowntime;
        downtimeRecord.ImpactLevel = ImpactLevel;

        downtimeRecord.UpdatedBy = UpdatedBy;
        downtimeRecord.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            downtimeRecord.Status = Status;
        }
    }
}

public sealed record DowntimeRecordResponse(
    string Id,
    string AssetId,
    string AssetName,
    DateTime? DowntimeStart,
    DateTime? DowntimeEnd,
    string? TotalDowntime,
    string? ReasonForDowntime,
    string? ImpactLevel,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static DowntimeRecordResponse FromEntity(DowntimeRecordEntity downtimeRecord)
    {
        return new DowntimeRecordResponse(
            downtimeRecord.Id ?? string.Empty,
            downtimeRecord.AssetId ?? string.Empty,
            downtimeRecord.AssetName ?? string.Empty,
            downtimeRecord.DowntimeStart,
            downtimeRecord.DowntimeEnd,
            downtimeRecord.TotalDowntime,
            downtimeRecord.ReasonForDowntime,
            downtimeRecord.ImpactLevel,
            downtimeRecord.CreatedBy,
            downtimeRecord.CreatedAt,
            downtimeRecord.UpdatedBy,
            downtimeRecord.UpdatedAt,
            downtimeRecord.ClientId,
            downtimeRecord.TenantId,
            downtimeRecord.Status,
            downtimeRecord.IsDeleted
        );
    }
}
