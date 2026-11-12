using System.Globalization;
using System.Text;
using A360.Domain.Entities;
using A360.EventLog.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.EventLog.Api.Endpoints;

/// <summary>
/// System Audit Log (Administration › System Audit Log): every event from every service, sorted into tabs (User
/// Activity, Configuration, Security, Data Changes, Integrations, Workflow, Imports/Exports) with severity and outcome.
/// "All Events" is every tab together.
/// </summary>
public static class AuditLogEndpoints
{
    private const string AllCategories = "all";
    private const int MaxPageSize = 200;
    private const int MaxExportRows = 50_000;
    private const int ExportBatchSize = 1_000;

    public static RouteGroupBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/audit-logs").WithTags("System Audit Log");

        group.MapGet("", GetAuditLogsAsync).WithName("GetAuditLogs");
        group.MapGet("/filters", GetFiltersAsync).WithName("GetAuditLogFilters");
        group.MapGet("/export", ExportAsync).WithName("ExportAuditLogs");
        group.MapGet("/{id}", GetAuditLogByIdAsync).WithName("GetAuditLogById");

        return group;
    }

    /// <summary>The Audit Events table for one tab, plus every tab's count under the same filters.</summary>
    private static async Task<IResult> GetAuditLogsAsync(
        [AsParameters] AuditLogQueryParameters query,
        IEventLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryBuildFilter(query, out var filter, out var category, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var page = Math.Max(query.Page ?? 1, 1);
        var pageSize = query.PageSize is null or < 1 or > MaxPageSize ? 25 : query.PageSize.Value;

        var result = await repository.QueryAuditAsync(filter, SortOf(query), page, pageSize, cancellationToken);
        var counts = await repository.CountByCategoryAsync(filter, cancellationToken);

        var categories = new List<AuditCategoryCount> { new(AllCategories, "All Events", counts.Values.Sum()) };
        categories.AddRange(AuditCategory.All.Select(tab => new AuditCategoryCount(tab.Key, tab.Label, counts.GetValueOrDefault(tab.Key))));

        return Results.Ok(new AuditLogListResponse(
            result.Items.Select(AuditLogItemResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize,
            (int)Math.Ceiling(result.TotalCount / (double)pageSize),
            category ?? AllCategories,
            categories));
    }

    private static async Task<IResult> GetAuditLogByIdAsync(
        string id,
        IEventLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid audit event id." });
        }

        var eventLog = await repository.GetByIdAsync(id, cancellationToken);
        return eventLog is null
            ? Results.NotFound(new { message = "Audit event not found." })
            : Results.Ok(AuditLogDetails.FromEntity(eventLog));
    }

    /// <summary>Tabs, event types, modules, users, severities and outcomes for the filter dropdowns.</summary>
    private static async Task<IResult> GetFiltersAsync(IEventLogRepository repository, CancellationToken cancellationToken)
    {
        var actions = await repository.DistinctValuesAsync(AuditLogField.Action, cancellationToken);
        var modules = await repository.DistinctValuesAsync(AuditLogField.Module, cancellationToken);
        var users = await repository.DistinctValuesAsync(AuditLogField.User, cancellationToken);

        var categories = new List<AuditFilterOption> { new(AllCategories, "All Events") };
        categories.AddRange(AuditCategory.All.Select(tab => new AuditFilterOption(tab.Key, tab.Label)));

        return Results.Ok(new AuditLogFiltersResponse(
            categories,
            actions.Select(action => new AuditFilterOption(action, EventTypeLabel(action))).ToList(),
            modules,
            users,
            AuditSeverity.All,
            AuditOutcome.All));
    }

    /// <summary>Export Log: the filtered events (current tab) as CSV, newest first, up to 50,000 rows.</summary>
    private static async Task<IResult> ExportAsync(
        [AsParameters] AuditLogQueryParameters query,
        IEventLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryBuildFilter(query, out var filter, out var category, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var sort = SortOf(query);
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp (UTC),Event,Category,Module,User,User ID,Role,Entity Type,Record ID,Record Name,Details,IP Address,Device / Browser,Severity,Outcome,Request ID,Event ID");

        var written = 0;
        for (var page = 1; written < MaxExportRows; page++)
        {
            var batch = await repository.QueryAuditAsync(filter, sort, page, ExportBatchSize, cancellationToken);
            foreach (var eventLog in batch.Items.Take(MaxExportRows - written))
            {
                AppendRow(csv,
                    eventLog.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    AuditClassifier.EventNameOf(eventLog.EntityType, eventLog.Action, eventLog.EntityName),
                    AuditCategory.LabelOf(eventLog.Category),
                    eventLog.Module,
                    eventLog.PerformedBy,
                    eventLog.PerformedByRefId,
                    eventLog.PerformedByRole,
                    eventLog.EntityType,
                    eventLog.EntityId,
                    eventLog.EntityName,
                    eventLog.Details,
                    eventLog.IpAddress,
                    AuditLogDetails.DescribeDevice(eventLog.UserAgent),
                    eventLog.Severity,
                    eventLog.Outcome,
                    eventLog.RequestId,
                    eventLog.Id);
                written++;
            }

            if (batch.Items.Count < ExportBatchSize)
            {
                break;
            }
        }

        var fileName = $"system-audit-log-{category ?? AllCategories}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return Results.File(bytes, "text/csv", fileName);
    }

    private static bool TryBuildFilter(
        AuditLogQueryParameters query,
        out AuditLogFilter filter,
        out string? category,
        out string? error)
    {
        filter = new AuditLogFilter();
        category = null;
        error = null;

        if (!string.IsNullOrWhiteSpace(query.Category)
            && !string.Equals(query.Category.Trim(), AllCategories, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(query.Category.Trim(), "All Events", StringComparison.OrdinalIgnoreCase))
        {
            category = AuditCategory.Normalize(query.Category);
            if (category is null)
            {
                error = $"Unknown category '{query.Category}'. Use all, {string.Join(", ", AuditCategory.All.Select(tab => tab.Key))}.";
                return false;
            }
        }

        string? severity = null;
        if (!IsAll(query.Severity))
        {
            severity = AuditSeverity.Normalize(query.Severity);
            if (severity is null)
            {
                error = $"Unknown severity '{query.Severity}'. Use {string.Join(", ", AuditSeverity.All)}.";
                return false;
            }
        }

        string? outcome = null;
        if (!IsAll(query.Outcome))
        {
            outcome = AuditOutcome.Normalize(query.Outcome);
            if (outcome is null)
            {
                error = $"Unknown outcome '{query.Outcome}'. Use {string.Join(", ", AuditOutcome.All)}.";
                return false;
            }
        }

        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        {
            error = "'from' must be on or before 'to'.";
            return false;
        }

        filter = new AuditLogFilter(
            category,
            ToUtc(query.From),
            ToUtc(query.To),
            IsAll(query.EventType) ? null : query.EventType,
            IsAll(query.Module) ? null : query.Module,
            IsAll(query.User) ? null : query.User,
            severity,
            query.IpAddress,
            outcome,
            query.Search,
            query.EntityType,
            query.EntityId);
        return true;
    }

    /// <summary>Dropdown placeholders ("All Types", "All Modules", …) mean no filter.</summary>
    private static bool IsAll(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().StartsWith("All ", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("all", StringComparison.OrdinalIgnoreCase);

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc)
    };

    private static AuditLogSort SortOf(AuditLogQueryParameters query)
    {
        var field = query.SortBy?.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant() switch
        {
            "event" or "eventtype" or "action" => AuditLogSortField.Event,
            "module" => AuditLogSortField.Module,
            "user" or "performedby" => AuditLogSortField.User,
            "ip" or "ipaddress" => AuditLogSortField.IpAddress,
            "severity" => AuditLogSortField.Severity,
            "outcome" => AuditLogSortField.Outcome,
            _ => AuditLogSortField.Timestamp
        };

        var descending = !string.Equals(query.SortDir?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        return new AuditLogSort(field, descending);
    }

    /// <summary>"LoginFailed" → "Login failed", "Created" → "Created".</summary>
    private static string EventTypeLabel(string action) => action switch
    {
        EventAction.LoggedIn => "Signed in",
        EventAction.LoggedOut => "Signed out",
        EventAction.LoginFailed => "Failed login",
        EventAction.OtpSent => "Sign-in code sent",
        EventAction.OtpSendFailed => "Sign-in code not sent",
        EventAction.OtpFailed => "Wrong sign-in code",
        EventAction.OtpLocked => "Sign-in locked",
        EventAction.TotpSetupStarted => "Authenticator setup started",
        EventAction.TotpEnabled => "Authenticator enabled",
        EventAction.TokenRefreshed => "Session refreshed",
        EventAction.TokenRefreshFailed => "Session refresh failed",
        EventAction.TokenReuseDetected => "Session token reuse",
        _ => AuditClassifier.Humanize(action)
    };

    private static void AppendRow(StringBuilder csv, params string?[] values)
    {
        csv.AppendLine(string.Join(',', values.Select(Escape)));

        // Quote when needed; a leading = + - @ is prefixed so spreadsheets don't run it as a formula
        static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value[0] is '=' or '+' or '-' or '@')
            {
                value = "'" + value;
            }

            return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }
    }
}
