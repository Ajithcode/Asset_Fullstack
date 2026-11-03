using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>IoT Tracking Dashboard → Alerts KPI and Recent Alerts &amp; Exceptions, addressed by alert id (e.g. AL000001).</summary>
public static class IoTAlertEndpoints
{
    private const string EntityType = "IoTAlert";
    private const string SequenceName = "IoTAlert";
    private const string AlertIdPrefix = "AL";

    public static RouteGroupBuilder MapIoTAlertEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-alerts").WithTags("IoT Alerts");

        group.MapGet("", GetAlertsAsync).WithName("GetIoTAlerts");
        group.MapGet("/count", GetAlertCountAsync).WithName("GetIoTAlertCount");
        group.MapGet("/options", () => Results.Ok(IoTAlertOptionsResponse.Instance)).WithName("GetIoTAlertOptions");
        group.MapGet("/{alertId}", GetAlertAsync).WithName("GetIoTAlertById");
        group.MapPost("", CreateAlertAsync).WithName("CreateIoTAlert");
        group.MapPut("/{alertId}", UpdateAlertAsync).WithName("UpdateIoTAlert");
        group.MapPatch("/{alertId}/status", UpdateAlertStatusAsync).WithName("UpdateIoTAlertStatus");
        group.MapDelete("/{alertId}", DeleteAlertAsync).WithName("DeleteIoTAlert");

        return group;
    }

    /// <summary>
    /// Most recently raised first. <paramref name="days"/> limits to the last N days (all time when omitted);
    /// <paramref name="limit"/> caps the rows, e.g. 5 for the dashboard table.
    /// </summary>
    private static async Task<IResult> GetAlertsAsync(
        IIoTAlertRepository repository,
        CancellationToken cancellationToken,
        int? days = null,
        string? status = null,
        string? severity = null,
        string? siteName = null,
        string? assetRegistrationId = null,
        string? search = null,
        int? limit = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        if (limit is < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["limit must be at least 1"] });
        }

        var period = days is null ? null : DashboardPeriod.LastDays(days);
        var alerts = await repository.QueryAsync(
            new IoTAlertQuery(period?.FromUtc, period?.ToUtc, Option(status, IoTAlertStatus.All), Option(severity, IoTAlertSeverity.All),
                siteName, assetRegistrationId, search),
            cancellationToken);

        return Results.Ok(alerts
            .Take(limit ?? int.MaxValue)
            .Select(IoTAlertResponse.FromEntity)
            .ToList());
    }

    /// <summary>The Alerts KPI card: alerts raised in the last <paramref name="days"/> days (default 7) vs. the period before.</summary>
    private static async Task<IResult> GetAlertCountAsync(
        IIoTAlertRepository repository,
        CancellationToken cancellationToken,
        int? days = null,
        string? siteName = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = DashboardPeriod.LastDays(days);
        var current = await repository.QueryAsync(new IoTAlertQuery(period.FromUtc, period.ToUtc, SiteName: siteName), cancellationToken);
        var previous = await repository.QueryAsync(new IoTAlertQuery(period.PreviousFromUtc, period.FromUtc, SiteName: siteName), cancellationToken);

        return Results.Ok(new IoTAlertCountResponse(
            period.Days,
            period.FromUtc,
            period.ToUtc,
            current.Count,
            previous.Count,
            DashboardPeriod.ChangePercent(current.Count, previous.Count),
            IoTAlertStatus.All.ToDictionary(option => option, option => current.Count(alert => alert.Status == option)),
            IoTAlertSeverity.All.ToDictionary(option => option, option => current.Count(alert => alert.Severity == option))));
    }

    private static async Task<IResult> GetAlertAsync(
        string alertId,
        IIoTAlertRepository repository,
        CancellationToken cancellationToken)
    {
        var alert = await repository.GetByAlertIdAsync(alertId.Trim(), cancellationToken);
        return alert is null ? NotFound(alertId) : Results.Ok(IoTAlertResponse.FromEntity(alert));
    }

    private static async Task<IResult> CreateAlertAsync(
        CreateIoTAlertRequest request,
        IIoTAlertRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var alert = request.ToEntity($"{AlertIdPrefix}{nextSequence:D6}", currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(alert, cancellationToken);
        await eventLogger.LogAsync(EntityType, alert.AlertId, Describe(alert), EventAction.Created, cancellationToken);

        return Results.Created($"/api/iot-alerts/{alert.AlertId}", IoTAlertResponse.FromEntity(alert));
    }

    /// <summary>Takes the same body as create; the alert keeps its alert id and acknowledgement / resolution stamps.</summary>
    private static async Task<IResult> UpdateAlertAsync(
        string alertId,
        CreateIoTAlertRequest request,
        IIoTAlertRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByAlertIdAsync(alertId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(alertId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var alert = request.ToEntity(existing.AlertId, currentUser).Replacing(existing, currentUser);
        alert.AcknowledgedAt = existing.AcknowledgedAt;
        alert.AcknowledgedBy = existing.AcknowledgedBy;
        alert.ResolvedAt = existing.ResolvedAt;
        alert.ResolvedBy = existing.ResolvedBy;
        alert.ApplyStatus(ContractText.Canonical(request.Status, IoTAlertStatus.All) ?? existing.Status ?? IoTAlertStatus.Open, currentUser);

        if (!await repository.UpdateAsync(existing.Id, alert, cancellationToken))
        {
            return NotFound(alertId);
        }

        await eventLogger.LogAsync(EntityType, alert.AlertId, Describe(alert), EventAction.Updated, cancellationToken);
        return Results.Ok(IoTAlertResponse.FromEntity(alert));
    }

    /// <summary>The table's Actions: acknowledge, start, resolve or reopen an alert.</summary>
    private static async Task<IResult> UpdateAlertStatusAsync(
        string alertId,
        UpdateIoTAlertStatusRequest request,
        IIoTAlertRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var alert = await repository.GetByAlertIdAsync(alertId.Trim(), cancellationToken);
        if (alert is null)
        {
            return NotFound(alertId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        alert.ApplyStatus(ContractText.Canonical(request.Status, IoTAlertStatus.All)!, currentUser);
        alert.UpdatedBy = currentUser?.UserName;
        alert.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(alert.Id, alert, cancellationToken))
        {
            return NotFound(alertId);
        }

        await eventLogger.LogAsync(EntityType, alert.AlertId, $"{Describe(alert)} → {alert.Status}", EventAction.Updated, cancellationToken);
        return Results.Ok(IoTAlertResponse.FromEntity(alert));
    }

    private static async Task<IResult> DeleteAlertAsync(
        string alertId,
        IIoTAlertRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var alert = await repository.GetByAlertIdAsync(alertId.Trim(), cancellationToken);
        if (alert is null || !await repository.DeleteAsync(alert.Id, cancellationToken))
        {
            return NotFound(alertId);
        }

        await eventLogger.LogAsync(EntityType, alert.AlertId, Describe(alert), EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // Filters accept any casing ("open" → "Open"); an unknown value matches nothing rather than everything.
    private static string? Option(string? value, IReadOnlyList<string> allowed)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ContractText.Canonical(value, allowed) ?? value.Trim();
    }

    private static string Describe(IoTAlert alert) => $"{alert.AssetName} - {alert.EventType} ({alert.Severity})";

    private static IResult NotFound(string alertId)
    {
        return Results.NotFound(new { message = $"IoT alert '{alertId}' was not found." });
    }
}
