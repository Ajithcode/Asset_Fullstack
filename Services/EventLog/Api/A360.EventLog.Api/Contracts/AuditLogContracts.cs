using System.Text.RegularExpressions;
using A360.Domain.Entities;
using EventLogEntity = A360.Domain.Entities.EventLog;

namespace A360.EventLog.Api.Contracts;

/// <summary>
/// Query string of GET /api/audit-logs and /api/audit-logs/export. <see cref="Category"/> is the tab: "all" (or empty)
/// for All Events, otherwise a key such as "user-activity" or its label ("User Activity").
/// </summary>
public sealed class AuditLogQueryParameters
{
    public string? Category { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Event Type dropdown: an action such as Created, Updated, Deleted, Viewed, LoggedIn, LoginFailed.</summary>
    public string? EventType { get; set; }

    public string? Module { get; set; }

    /// <summary>User name, user ref id or email.</summary>
    public string? User { get; set; }

    public string? Severity { get; set; }
    public string? IpAddress { get; set; }
    public string? Outcome { get; set; }
    public string? Search { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>timestamp (default), event, module, user, ipAddress, severity, outcome.</summary>
    public string? SortBy { get; set; }

    /// <summary>desc (default) or asc.</summary>
    public string? SortDir { get; set; }

    /// <summary>1-based; default 1.</summary>
    public int? Page { get; set; }

    /// <summary>1-200; default 25.</summary>
    public int? PageSize { get; set; }
}

/// <summary>One row of the Audit Events table. <see cref="Record"/> is the Entity / Record column: the name, else the id.</summary>
public sealed record AuditLogItemResponse(
    string Id,
    DateTime? Timestamp,
    string Event,
    string Action,
    string? Category,
    string CategoryLabel,
    string? Module,
    string? User,
    string? UserId,
    string EntityType,
    string EntityId,
    string? EntityName,
    string Record,
    string? IpAddress,
    string? Severity,
    string? Outcome,
    string? Details)
{
    public static AuditLogItemResponse FromEntity(EventLogEntity eventLog) => new(
        eventLog.Id,
        eventLog.CreatedAt,
        AuditClassifier.EventNameOf(eventLog.EntityType, eventLog.Action, eventLog.EntityName),
        eventLog.Action,
        eventLog.Category,
        AuditCategory.LabelOf(eventLog.Category),
        eventLog.Module,
        eventLog.PerformedBy,
        eventLog.PerformedByRefId,
        eventLog.EntityType,
        eventLog.EntityId,
        string.IsNullOrWhiteSpace(eventLog.EntityName) ? null : eventLog.EntityName,
        string.IsNullOrWhiteSpace(eventLog.EntityName) ? eventLog.EntityId : eventLog.EntityName,
        eventLog.IpAddress,
        eventLog.Severity,
        eventLog.Outcome,
        eventLog.Details);
}

/// <summary>A tab with its event count under the current filters. Key "all" is All Events.</summary>
public sealed record AuditCategoryCount(string Key, string Label, long Count);

public sealed record AuditLogListResponse(
    IReadOnlyList<AuditLogItemResponse> Items,
    long TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    string Category,
    IReadOnlyList<AuditCategoryCount> Categories);

/// <summary>GET /api/audit-logs/{id}: the event detail panel.</summary>
public sealed record AuditLogDetailResponse(
    string Id,
    DateTime? Timestamp,
    string Event,
    string? Category,
    string CategoryLabel,
    string? Severity,
    string? Outcome,
    string Message,
    string? Details,
    AuditActorContext Actor);

/// <summary>Actor &amp; Context section. Values not recorded for an event (older events, background jobs) are null.</summary>
public sealed record AuditActorContext(
    string? User,
    string? UserId,
    string? Email,
    string? Role,
    string? Module,
    string Action,
    string EntityType,
    string RecordId,
    string? RecordName,
    string? IpAddress,
    string? DeviceBrowser,
    string? UserAgent,
    string? RequestId,
    string ServiceName);

public sealed record AuditFilterOption(string Value, string Label);

/// <summary>GET /api/audit-logs/filters: values for the filter dropdowns and tabs.</summary>
public sealed record AuditLogFiltersResponse(
    IReadOnlyList<AuditFilterOption> Categories,
    IReadOnlyList<AuditFilterOption> EventTypes,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Users,
    IReadOnlyList<string> Severities,
    IReadOnlyList<string> Outcomes);

public static class AuditLogDetails
{
    public static AuditLogDetailResponse FromEntity(EventLogEntity eventLog) => new(
        eventLog.Id,
        eventLog.CreatedAt,
        AuditClassifier.EventNameOf(eventLog.EntityType, eventLog.Action, eventLog.EntityName),
        eventLog.Category,
        AuditCategory.LabelOf(eventLog.Category),
        eventLog.Severity,
        eventLog.Outcome,
        MessageOf(eventLog),
        eventLog.Details,
        new AuditActorContext(
            eventLog.PerformedBy,
            eventLog.PerformedByRefId,
            eventLog.PerformedByEmail,
            eventLog.PerformedByRole,
            eventLog.Module,
            eventLog.Action,
            eventLog.EntityType,
            eventLog.EntityId,
            string.IsNullOrWhiteSpace(eventLog.EntityName) ? null : eventLog.EntityName,
            eventLog.IpAddress,
            DescribeDevice(eventLog.UserAgent),
            eventLog.UserAgent,
            eventLog.RequestId,
            eventLog.ServiceName));

    /// <summary>"Asset registration updated: Pump 01 (AST-…) by Aisha Rahman".</summary>
    public static string MessageOf(EventLogEntity eventLog)
    {
        var name = AuditClassifier.EventNameOf(eventLog.EntityType, eventLog.Action, eventLog.EntityName);
        var record = string.IsNullOrWhiteSpace(eventLog.EntityName) || eventLog.EntityName == eventLog.EntityId
            ? eventLog.EntityId
            : $"{eventLog.EntityName} ({eventLog.EntityId})";
        var message = string.IsNullOrWhiteSpace(record) ? name : $"{name}: {record}";
        return string.IsNullOrWhiteSpace(eventLog.PerformedBy) ? message : $"{message} by {eventLog.PerformedBy}";
    }

    /// <summary>"macOS / Safari 18.2", "Windows / Chrome 131" from a User-Agent header; null when there is none.</summary>
    public static string? DescribeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var os = userAgent switch
        {
            _ when userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) => "Windows",
            _ when userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) => "iOS",
            _ when userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) => "macOS",
            _ when userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) => "Android",
            _ when userAgent.Contains("CrOS", StringComparison.OrdinalIgnoreCase) => "ChromeOS",
            _ when userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) => "Linux",
            _ => null
        };

        var browser = Match(userAgent, @"Edg(?:e|A|iOS)?/([\d.]+)", "Edge")
            ?? Match(userAgent, @"(?:OPR|Opera)/([\d.]+)", "Opera")
            ?? Match(userAgent, @"(?:Firefox|FxiOS)/([\d.]+)", "Firefox")
            ?? Match(userAgent, @"(?:Chrome|CriOS)/([\d.]+)", "Chrome")
            ?? (userAgent.Contains("Safari/", StringComparison.Ordinal) ? Match(userAgent, @"Version/([\d.]+)", "Safari") : null)
            ?? Match(userAgent, @"^(PostmanRuntime|curl|okhttp|python-requests|Dart)/([\d.]+)", null);

        return (os, browser) switch
        {
            (null, null) => userAgent.Length <= 60 ? userAgent : userAgent[..60],
            (null, _) => browser,
            (_, null) => os,
            _ => $"{os} / {browser}"
        };

        static string? Match(string value, string pattern, string? name)
        {
            var match = Regex.Match(value, pattern);
            if (!match.Success)
            {
                return null;
            }

            // Name from the pattern (tools), else the given name; major.minor version only
            var version = match.Groups[match.Groups.Count - 1].Value;
            var parts = version.Split('.');
            var shortVersion = parts.Length > 1 ? $"{parts[0]}.{parts[1]}" : version;
            if (shortVersion.EndsWith(".0", StringComparison.Ordinal))
            {
                shortVersion = shortVersion[..^2];
            }

            return $"{name ?? match.Groups[1].Value} {shortVersion}";
        }
    }
}
