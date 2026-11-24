using System.Globalization;
using System.Net;
using A360.ReportAnalytics.Api.Analytics;
using A360.ReportAnalytics.Api.Contracts;
using A360.ReportAnalytics.Domain.Entities;
using A360.ReportAnalytics.Repository.Repositories;

namespace A360.ReportAnalytics.Api.Scheduling;

public sealed record ReportRunResult(bool Sent, string? Error, string? FileName, int RecordCount, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>Generates a report for its schedule's period, in the schedule's format, and mails it to the recipients.</summary>
public sealed class ScheduledReportRunner(
    IStandardReportRepository reports,
    IAssetMovementReportRepository movementRepository,
    IReportMailer mailer,
    ILogger<ScheduledReportRunner> logger)
{
    public async Task<ReportRunResult> RunAsync(StandardReport report, DateTime runAtUtc, CancellationToken cancellationToken)
    {
        var schedule = report.Schedule!;
        ReportPeriods.TryGetTimeZone(report.TimeZone, out var timeZone);
        var runDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(runAtUtc, DateTimeKind.Utc), timeZone));
        var (start, end) = ReportScheduleCalculator.PeriodFor(schedule.Frequency, runDate);

        ReportRunResult result;
        try
        {
            var table = await BuildTableAsync(report, start, end, cancellationToken);
            var file = ReportFileBuilder.Build(table.Table, schedule.Format, FileName(report, start, end));

            await mailer.SendAsync(
                schedule.Recipients,
                $"{report.ReportName} ({Period(start, end)})",
                EmailBody(report, start, end, table.RecordCount, file.FileName),
                file,
                cancellationToken);

            result = new ReportRunResult(true, null, file.FileName, table.RecordCount, start, end);
            logger.LogInformation("Sent report {ReportId} ({Format}, {Count} rows) to {Recipients}",
                report.ReportId, schedule.Format, table.RecordCount, string.Join(", ", schedule.Recipients));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Scheduled report {ReportId} was not sent", report.ReportId);
            result = new ReportRunResult(false, exception.Message, null, 0, start, end);
        }

        await reports.SaveScheduleRunAsync(report.Id, runAtUtc, result.Sent, result.Error, CancellationToken.None);
        return result;
    }

    private async Task<(ReportTable Table, int RecordCount)> BuildTableAsync(StandardReport report, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        List<(string, string)> details =
        [
            ("Report ID", report.ReportId),
            ("Category", report.Category),
            ("Report Type", report.ReportType ?? "-"),
            ("Period", Period(start, end)),
            ("Time Zone", report.TimeZone),
            ("Generated On", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC")
        ];

        if (!string.Equals(report.ReportType, StandardReportOptions.AssetMovementType, StringComparison.OrdinalIgnoreCase))
        {
            // Only Asset Movement has data behind it so far; other types are sent with their details.
            var info = new ReportTable(
                report.ReportName,
                details,
                ["Report ID", "Report Name", "Category", "Report Type", "Description"],
                [[report.ReportId, report.ReportName, report.Category, report.ReportType ?? "-", report.Description ?? string.Empty]],
                $"No data is available yet for report type '{report.ReportType ?? "(none)"}'.");
            return (info, 0);
        }

        var built = await AssetMovementReport.BuildAsync(
            report,
            new AssetMovementReportRequest(StartDate: start, EndDate: end, TimeZone: report.TimeZone),
            movementRepository,
            report.TenantId,
            report.ClientId,
            cancellationToken);

        if (built.Errors is not null)
        {
            throw new InvalidOperationException(string.Join("; ", built.Errors.SelectMany(error => error.Value)));
        }

        var data = built.Data!;
        details.Add(("Records", data.TotalRecords.ToString(CultureInfo.InvariantCulture)));

        var rows = data.Dates
            .SelectMany(day => day.Records.Select(row => (IReadOnlyList<string>)
            [
                DateOnly.ParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                row.AssetId,
                row.AssetName ?? string.Empty,
                row.Category ?? string.Empty,
                row.SubCategory ?? string.Empty,
                row.TagId,
                row.ZoneName ?? string.Empty,
                row.DeviceId,
                row.ZoneInLocal,
                row.ZoneOutLocal,
                row.TimeSpent
            ]))
            .ToList();

        var table = new ReportTable(
            report.ReportName,
            details,
            ["Date", "Asset ID", "Asset Name", "Category", "Sub Category", "Tag ID", "Zone", "Gateway", "Zone In", "Zone Out", "Time Spent"],
            rows,
            data.Truncated ? "The period has more readings than one report can hold; only the earliest ones are included." : null);
        return (table, data.TotalRecords);
    }

    private static string EmailBody(StandardReport report, DateOnly start, DateOnly end, int recordCount, string fileName)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        return $"""
            <p>Hello,</p>
            <p>Your scheduled report <b>{E(report.ReportName)}</b> ({E(report.ReportId)}) is attached.</p>
            <table cellpadding="4" style="border-collapse:collapse">
              <tr><td><b>Category</b></td><td>{E(report.Category)}</td></tr>
              <tr><td><b>Report Type</b></td><td>{E(report.ReportType ?? "-")}</td></tr>
              <tr><td><b>Period</b></td><td>{E(Period(start, end))} ({E(report.TimeZone)})</td></tr>
              <tr><td><b>Records</b></td><td>{recordCount}</td></tr>
              <tr><td><b>File</b></td><td>{E(fileName)}</td></tr>
            </table>
            <p>This email was sent automatically by Asset360 Reports.</p>
            """;
    }

    private static string Period(DateOnly start, DateOnly end) =>
        start == end
            ? start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : $"{start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} - {end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    private static string FileName(StandardReport report, DateOnly start, DateOnly end)
    {
        var name = new string(report.ReportName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        return $"{report.ReportId}-{name}-{start:yyyyMMdd}-{end:yyyyMMdd}";
    }
}

/// <summary>Checks every minute for scheduled reports that are due and sends them.</summary>
public sealed class ReportScheduleWorker(IServiceScopeFactory scopeFactory, ILogger<ReportScheduleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunDueAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Report schedule check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IStandardReportRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<ScheduledReportRunner>();

        var now = DateTime.UtcNow;
        foreach (var report in await reports.GetDueSchedulesAsync(now, BatchSize, cancellationToken))
        {
            var due = report.NextScheduledOn!.Value;
            ReportPeriods.TryGetTimeZone(report.TimeZone, out var timeZone);

            // Next run counted from now, so a service that was down sends one catch-up email, not one per missed run.
            var next = ReportScheduleCalculator.Next(report.Schedule!, timeZone, now);
            if (!await reports.TryClaimScheduledRunAsync(report.Id, due, next, cancellationToken))
            {
                continue; // another instance took it
            }

            report.NextScheduledOn = next;
            await runner.RunAsync(report, now, cancellationToken);
        }
    }
}
