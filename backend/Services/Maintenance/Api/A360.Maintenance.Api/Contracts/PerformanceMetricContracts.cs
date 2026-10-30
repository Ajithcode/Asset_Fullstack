
using PerformanceMetricEntity = A360.Maintenance.Domain.Entities.PerformanceMetric;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreatePerformanceMetricRequest(
    string? AssetId,
    string? AssetName,
    double MtbfHrs,
    double MttrHrs,
    double AssetUptimePercent,
    string? MaintenanceFrequency,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public PerformanceMetricEntity ToEntity()
    {
        return new PerformanceMetricEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,
            MtbfHrs = MtbfHrs,
            MttrHrs = MttrHrs,
            AssetUptimePercent = AssetUptimePercent,
            MaintenanceFrequency = MaintenanceFrequency,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePerformanceMetricRequest(
    string? AssetId,
    string? AssetName,
    double MtbfHrs,
    double MttrHrs,
    double AssetUptimePercent,
    string? MaintenanceFrequency,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(PerformanceMetricEntity performanceMetric)
    {
        performanceMetric.AssetId = AssetId;
        performanceMetric.AssetName = AssetName;
        performanceMetric.MtbfHrs = MtbfHrs;
        performanceMetric.MttrHrs = MttrHrs;
        performanceMetric.AssetUptimePercent = AssetUptimePercent;
        performanceMetric.MaintenanceFrequency = MaintenanceFrequency;

        performanceMetric.UpdatedBy = UpdatedBy;
        performanceMetric.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            performanceMetric.Status = Status;
        }
    }
}

public sealed record PerformanceMetricResponse(
    string Id,
    string AssetId,
    string AssetName,
    double MtbfHrs,
    double MttrHrs,
    double AssetUptimePercent,
    string? MaintenanceFrequency,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PerformanceMetricResponse FromEntity(PerformanceMetricEntity performanceMetric)
    {
        return new PerformanceMetricResponse(
            performanceMetric.Id ?? string.Empty,
            performanceMetric.AssetId ?? string.Empty,
            performanceMetric.AssetName ?? string.Empty,
            performanceMetric.MtbfHrs,
            performanceMetric.MttrHrs,
            performanceMetric.AssetUptimePercent,
            performanceMetric.MaintenanceFrequency,
            performanceMetric.CreatedBy,
            performanceMetric.CreatedAt,
            performanceMetric.UpdatedBy,
            performanceMetric.UpdatedAt,
            performanceMetric.ClientId,
            performanceMetric.TenantId,
            performanceMetric.Status,
            performanceMetric.IsDeleted
        );
    }
}
