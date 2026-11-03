using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Records how long one asset ran and idled on one day. The UtilizationId (e.g. UT000001) is generated.</summary>
/// <param name="Date">The day; only the date part is kept. Defaults to today (UTC).</param>
/// <param name="RunningMinutes">0–1440; running + idle cannot exceed a day.</param>
public sealed record CreateAssetUtilizationRequest(
    DateTime? Date,
    string? AssetRegistrationId,
    string? AssetName,
    string? AssetType,
    string? DeviceId,
    string? SiteName,
    int? RunningMinutes,
    int? IdleMinutes,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    /// <remarks>
    /// A plain date ("2026-11-01") is taken as that day as-is; converting it to UTC first would shift it to the
    /// previous day on servers east of UTC. Only a value that carries a local offset is converted.
    /// </remarks>
    public DateTime DateUtc => Date switch
    {
        null => DateTime.UtcNow.Date,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime().Date,
        var value => value.Value.Date
    };

    public AssetUtilization ToEntity(string utilizationId, CurrentUser? currentUser)
    {
        return new AssetUtilization
        {
            UtilizationId = utilizationId,
            Date = DateTime.SpecifyKind(DateUtc, DateTimeKind.Utc),
            AssetRegistrationId = AssetRegistrationId!.Trim(),
            AssetName = ContractText.TrimOrNull(AssetName),
            AssetType = AssetType!.Trim(),
            DeviceId = ContractText.TrimOrNull(DeviceId),
            SiteName = ContractText.TrimOrNull(SiteName),
            RunningMinutes = RunningMinutes ?? 0,
            IdleMinutes = IdleMinutes ?? 0,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            Status = "Active",
            IsDeleted = false
        };
    }
}

public sealed record AssetUtilizationResponse(
    string Id,
    string UtilizationId,
    DateTime Date,
    string AssetRegistrationId,
    string? AssetName,
    string AssetType,
    string? DeviceId,
    string? SiteName,
    int RunningMinutes,
    int IdleMinutes,
    double RunningPercent,
    double IdlePercent,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static AssetUtilizationResponse FromEntity(AssetUtilization record)
    {
        var (running, idle) = UtilizationMath.Percentages(record.RunningMinutes, record.IdleMinutes);
        return new AssetUtilizationResponse(
            record.Id,
            record.UtilizationId,
            record.Date,
            record.AssetRegistrationId,
            record.AssetName,
            record.AssetType,
            record.DeviceId,
            record.SiteName,
            record.RunningMinutes,
            record.IdleMinutes,
            running,
            idle,
            record.CreatedBy,
            record.CreatedAt,
            record.UpdatedBy,
            record.UpdatedAt);
    }
}

/// <summary>One bar of "Utilization Rate (by Asset Type)": share of tracked time running vs. idle.</summary>
public sealed record AssetTypeUtilizationResponse(
    string AssetType,
    int AssetCount,
    double RunningHours,
    double IdleHours,
    double RunningPercent,
    double IdlePercent);

/// <summary>The Utilization Rate panel for the last <see cref="Days"/> days; bars sorted by running % (highest first).</summary>
public sealed record UtilizationByAssetTypeResponse(
    int Days,
    DateTime From,
    DateTime To,
    double OverallRunningPercent,
    double OverallIdlePercent,
    IReadOnlyList<AssetTypeUtilizationResponse> Items)
{
    public static UtilizationByAssetTypeResponse FromRecords(DashboardPeriod period, IReadOnlyCollection<AssetUtilization> records)
    {
        var items = records
            .GroupBy(record => record.AssetType, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var runningMinutes = group.Sum(record => record.RunningMinutes);
                var idleMinutes = group.Sum(record => record.IdleMinutes);
                var (running, idle) = UtilizationMath.Percentages(runningMinutes, idleMinutes);
                return new AssetTypeUtilizationResponse(
                    group.First().AssetType,
                    group.Select(record => record.AssetRegistrationId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Math.Round(runningMinutes / 60.0, 1),
                    Math.Round(idleMinutes / 60.0, 1),
                    running,
                    idle);
            })
            .OrderByDescending(item => item.RunningPercent)
            .ThenBy(item => item.AssetType)
            .ToList();

        var (overallRunning, overallIdle) = UtilizationMath.Percentages(
            records.Sum(record => record.RunningMinutes),
            records.Sum(record => record.IdleMinutes));

        return new UtilizationByAssetTypeResponse(period.Days, period.FromUtc, period.ToUtc, overallRunning, overallIdle, items);
    }
}

internal static class UtilizationMath
{
    /// <summary>Running and idle as % of tracked time (running + idle), 1 decimal; 0 / 0 when nothing was tracked.</summary>
    public static (double Running, double Idle) Percentages(long runningMinutes, long idleMinutes)
    {
        var tracked = runningMinutes + idleMinutes;
        if (tracked == 0)
        {
            return (0, 0);
        }

        var running = Math.Round(runningMinutes * 100.0 / tracked, 1);
        return (running, Math.Round(100 - running, 1));
    }
}
