using A360.ReportAnalytics.Domain.Entities;

namespace A360.ReportAnalytics.Api.Contracts;

/// <summary>
/// Dates without an offset are read in TimeZone (default UTC). When IncludeTime is false the
/// report period is stored as whole days.
/// </summary>
public sealed record CreateStandardReportRequest(
    string? ReportName,
    string? Description,
    string? Category,
    DateTime? ReportPeriodFrom,
    DateTime? ReportPeriodTo,
    string? TimeZone,
    bool? IncludeTime,
    string? Frequency,
    DateTime? NextScheduledOn,
    DateTime? ReviewDueOn,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId,
    string? ReportType = null);

public sealed record UpdateStandardReportRequest(
    string? ReportName,
    string? Description,
    string? Category,
    DateTime? ReportPeriodFrom,
    DateTime? ReportPeriodTo,
    string? TimeZone,
    bool? IncludeTime,
    string? Frequency,
    DateTime? NextScheduledOn,
    DateTime? ReviewDueOn,
    string? Status,
    string? UpdatedBy,
    string? ReportType = null);

/// <summary>Schedule action. On demand clears the next run.</summary>
/// <summary>
/// Schedule Report: the report is generated and emailed to Recipients at Time on the picked Days.
/// </summary>
/// <param name="Frequency">Daily, Weekly or Monthly; "On demand" stops the schedule.</param>
/// <param name="Days">Weekly: week days, e.g. ["Monday","Thursday"]. Monthly: dates 1-31, e.g. [1, 15] (31 runs on the last day of
/// shorter months). Not needed for Daily.</param>
/// <param name="Time">HH:mm in the report's time zone, e.g. "09:30".</param>
/// <param name="Format">xlsx (Excel), csv or pdf.</param>
/// <param name="Recipients">Email addresses the report is sent to.</param>
/// <param name="NextScheduledOn">Ignored (any value or format is accepted): the next run is worked out from the schedule.</param>
public sealed record ScheduleStandardReportRequest(
    string? Frequency,
    System.Text.Json.JsonElement? NextScheduledOn,
    string? TimeZone,
    string? UpdatedBy,
    IReadOnlyList<System.Text.Json.JsonElement>? Days = null,
    string? Time = null,
    string? Format = null,
    IReadOnlyList<string>? Recipients = null);

/// <summary>The saved schedule, with the outcome of its last run.</summary>
public sealed record StandardReportScheduleResponse(
    bool Enabled,
    string Frequency,
    IReadOnlyList<string> WeekDays,
    IReadOnlyList<int> MonthDays,
    string Time,
    string Format,
    IReadOnlyList<string> Recipients,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    string? LastRunError)
{
    public static StandardReportScheduleResponse? FromEntity(StandardReport report) =>
        report.Schedule is not { } schedule
            ? null
            : new StandardReportScheduleResponse(
                schedule.Enabled,
                schedule.Frequency,
                schedule.WeekDays,
                schedule.MonthDays,
                schedule.Time,
                schedule.Format,
                schedule.Recipients,
                schedule.Enabled ? report.NextScheduledOn : null,
                schedule.LastRunAt,
                schedule.LastRunStatus,
                schedule.LastRunError);
}

/// <summary>Send Now: the outcome of generating and mailing the report straight away.</summary>
public sealed record StandardReportSendResponse(
    bool Sent,
    string? Error,
    string? FileName,
    int RecordCount,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<string> Recipients);

public sealed record StandardReportScheduleOptionsResponse(
    IReadOnlyList<string> Frequencies,
    IReadOnlyList<string> WeekDays,
    IReadOnlyList<int> MonthDays,
    IReadOnlyList<string> Formats);

/// <summary>Reporting period card: applies one period to many reports at once (Apply Dates).</summary>
public sealed record ApplyReportPeriodRequest(
    IReadOnlyList<string>? ReportIds,
    DateTime? ReportPeriodFrom,
    DateTime? ReportPeriodTo,
    string? TimeZone,
    bool? IncludeTime,
    string? UpdatedBy);

public sealed record StandardReportResponse(
    string ReportId,
    string ReportName,
    string? Description,
    string Category,
    string? ReportType,
    DateTime? ReportPeriodFrom,
    DateTime? ReportPeriodTo,
    string TimeZone,
    bool IncludeTime,
    bool HasDateFilter,
    DateTime? GeneratedOn,
    DateTime? NextScheduledOn,
    string Frequency,
    DateTime? ReviewDueOn,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId,
    StandardReportScheduleResponse? Schedule)
{
    public static StandardReportResponse FromEntity(StandardReport report)
    {
        return new StandardReportResponse(
            report.ReportId,
            report.ReportName,
            report.Description,
            report.Category,
            report.ReportType,
            report.ReportPeriodFrom,
            report.ReportPeriodTo,
            report.TimeZone,
            report.IncludeTime,
            report.ReportPeriodFrom is not null && report.ReportPeriodTo is not null,
            report.GeneratedOn,
            report.NextScheduledOn,
            report.Frequency,
            report.ReviewDueOn,
            report.Status,
            report.CreatedBy,
            report.CreatedAt,
            report.UpdatedBy,
            report.UpdatedAt,
            report.TenantId,
            report.ClientId,
            StandardReportScheduleResponse.FromEntity(report));
    }
}

public sealed record StandardReportListResponse(
    IReadOnlyCollection<StandardReportResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>The four cards at the top of the page.</summary>
public sealed record StandardReportSummaryResponse(
    long TotalReports,
    long WithDateFilters,
    long Scheduled,
    long UpdatedThisMonth);

/// <summary>A day on the calendar card with its Generated / Scheduled / Review due markers.</summary>
public sealed record StandardReportCalendarDay(
    DateOnly Date,
    int Generated,
    int Scheduled,
    int ReviewDue);

public sealed record StandardReportCalendarResponse(
    int Year,
    int Month,
    string TimeZone,
    IReadOnlyCollection<StandardReportCalendarDay> Days);

/// <summary>A quick period button resolved to dates in the requested zone.</summary>
public sealed record ReportPeriodResponse(
    string Preset,
    DateOnly From,
    DateOnly To,
    string TimeZone);

public sealed record ApplyReportPeriodResponse(
    int UpdatedCount,
    IReadOnlyCollection<string> NotFound);

/// <summary>A saved report with its rows for the period. Data is null for a report type without data behind it.</summary>
public sealed record StandardReportDetailsResponse(
    StandardReportResponse Report,
    AssetMovementReportResponse? Data);
