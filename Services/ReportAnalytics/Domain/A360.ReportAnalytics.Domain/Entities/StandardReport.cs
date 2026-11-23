using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.ReportAnalytics.Domain.Entities;

/// <summary>A row on Reports &amp; Analytics / Standard Reports. Status (Draft, Ready, Scheduled, Needs review) lives on BaseEntity.</summary>
[BsonIgnoreExtraElements]
public sealed class StandardReport : BaseEntity
{
    // Business identifier exposed to clients (e.g. RPT-0001); kept separate from the Mongo _id.
    [BsonElement("report_id")]
    public string ReportId { get; set; } = string.Empty;

    [BsonElement("report_name")]
    public string ReportName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    // What the report shows (e.g. Asset Movement); decides the rows GET /api/standard-reports/{id}/details returns.
    // Null for reports that only carry their schedule and period.
    [BsonElement("report_type")]
    public string? ReportType { get; set; }

    // Report Period column; both are null when the report has no date filter. Stored in UTC.
    [BsonElement("report_period_from")]
    public DateTime? ReportPeriodFrom { get; set; }

    [BsonElement("report_period_to")]
    public DateTime? ReportPeriodTo { get; set; }

    // IANA or Windows id (e.g. Asia/Dubai) the period and schedule were entered in.
    [BsonElement("time_zone")]
    public string TimeZone { get; set; } = "UTC";

    // False when the period is whole days, so clients show dates without times.
    [BsonElement("include_time")]
    public bool IncludeTime { get; set; }

    [BsonElement("frequency")]
    public string Frequency { get; set; } = string.Empty;

    [BsonElement("generated_on")]
    public DateTime? GeneratedOn { get; set; }

    [BsonElement("next_scheduled_on")]
    public DateTime? NextScheduledOn { get; set; }

    // "Review due" marker on the calendar.
    [BsonElement("review_due_on")]
    public DateTime? ReviewDueOn { get; set; }

    // Email delivery: when set (and enabled), the report is generated at NextScheduledOn and mailed to the recipients.
    [BsonElement("schedule")]
    [BsonIgnoreIfNull]
    public StandardReportSchedule? Schedule { get; set; }
}

/// <summary>
/// Schedule Report: Frequency, Day, Time, Format and Recipients. Day is the week days (Weekly) or the days of the month
/// (Monthly); Daily runs every day. Time is HH:mm in the report's time zone. The next run is kept in StandardReport.NextScheduledOn.
/// </summary>
public sealed class StandardReportSchedule
{
    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("frequency")]
    public string Frequency { get; set; } = string.Empty;

    /// <summary>Weekly: "Monday" … "Sunday".</summary>
    [BsonElement("week_days")]
    public List<string> WeekDays { get; set; } = [];

    /// <summary>Monthly: 1-31; a day past the end of a month runs on its last day.</summary>
    [BsonElement("month_days")]
    public List<int> MonthDays { get; set; } = [];

    /// <summary>HH:mm in the report's time zone.</summary>
    [BsonElement("time")]
    public string Time { get; set; } = "09:00";

    /// <summary>xlsx, csv or pdf.</summary>
    [BsonElement("format")]
    public string Format { get; set; } = "xlsx";

    [BsonElement("recipients")]
    public List<string> Recipients { get; set; } = [];

    [BsonElement("last_run_at")]
    public DateTime? LastRunAt { get; set; }

    /// <summary>"Sent" or "Failed".</summary>
    [BsonElement("last_run_status")]
    public string? LastRunStatus { get; set; }

    [BsonElement("last_run_error")]
    public string? LastRunError { get; set; }
}
