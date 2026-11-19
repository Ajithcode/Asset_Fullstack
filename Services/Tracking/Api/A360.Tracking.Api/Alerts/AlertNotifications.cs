using System.Security.Cryptography;
using A360.Tracking.Domain.Entities;
using MongoDB.Driver;

namespace A360.Tracking.Api.Alerts;

/// <summary>
/// Desktop notifications of alerts. An alert raised by a rule with the Push channel gets a schedule: the first
/// notification right away, then one every rule.NotifyIntervalSeconds until rule.NotifyRepeat were sent (0 = until
/// the alert is acknowledged). The notification's Remind me button sends it again after the interval; Acknowledge
/// acknowledges the alert and stops the schedule.
/// </summary>
public sealed class AlertNotifications(IMongoDatabase database)
{
    /// <summary>Remind me waits at least this long, even when the rule repeats faster.</summary>
    public static readonly TimeSpan MinRemindDelay = TimeSpan.FromSeconds(30);

    private IMongoCollection<AlertNotificationDocument> Schedules =>
        database.GetCollection<AlertNotificationDocument>(AlertNotificationDocument.CollectionName);

    private IMongoCollection<IoTAlert> Alerts => database.GetCollection<IoTAlert>("IoTAlert");

    /// <summary>Starts the schedule of a new alert when its rule sends desktop notifications.</summary>
    public async Task ScheduleAsync(IoTAlert alert, AlertRule? rule, CancellationToken cancellationToken)
    {
        if (rule is null || !rule.NotificationChannels.Contains(AlertNotificationChannel.Push))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await Schedules.InsertOneAsync(new AlertNotificationDocument
        {
            AlertId = alert.AlertId,
            AlertRuleId = rule.Id,
            ClientId = alert.ClientId ?? rule.ClientId,
            TenantId = alert.TenantId ?? rule.TenantId,
            Title = $"{alert.EventType}: {alert.AssetName}",
            Body = alert.Message ?? $"{alert.AssetName} · {rule.GeofenceName}",
            Severity = rule.Severity,
            Sound = rule.NotifySound,
            IntervalSeconds = Math.Max(1, rule.NotifyIntervalSeconds),
            Repeat = Math.Max(0, rule.NotifyRepeat),
            NextAt = now,
            ActionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken: cancellationToken);
    }

    /// <summary>Remind me / Acknowledge from a notification. False when the alert or token does not match.</summary>
    public async Task<bool> ApplyActionAsync(string alertId, string token, string action, string? userName, CancellationToken cancellationToken)
    {
        var schedule = await Schedules.Find(s => s.AlertId == alertId && s.ActionToken == token).FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (action == "acknowledge")
        {
            var alert = await Alerts.Find(a => a.AlertId == alertId && !a.IsDeleted).FirstOrDefaultAsync(cancellationToken);
            if (alert is not null && alert.Status == IoTAlertStatus.Open)
            {
                await Alerts.UpdateOneAsync(a => a.Id == alert.Id, Builders<IoTAlert>.Update
                    .Set(a => a.Status, IoTAlertStatus.Acknowledged)
                    .Set(a => a.AcknowledgedAt, now)
                    .Set(a => a.AcknowledgedBy, string.IsNullOrWhiteSpace(userName) ? "Desktop notification" : userName)
                    .Push(a => a.History, new IoTAlertHistoryEntry { At = now, By = string.IsNullOrWhiteSpace(userName) ? "Desktop notification" : userName, Action = IoTAlertStatus.Acknowledged, Note = "From the desktop notification" })
                    .Set(a => a.UpdatedAt, now)
                    .Set(a => a.UpdatedBy, userName), cancellationToken: cancellationToken);
            }

            await Schedules.UpdateOneAsync(s => s.Id == schedule.Id, Builders<AlertNotificationDocument>.Update
                .Set(s => s.Done, true).Set(s => s.NextAt, null).Set(s => s.UpdatedAt, now), cancellationToken: cancellationToken);
            return true;
        }

        // remind: send again after the interval (at least MinRemindDelay), even when the repeat count was used up
        var delay = TimeSpan.FromSeconds(Math.Max(schedule.IntervalSeconds, MinRemindDelay.TotalSeconds));
        await Schedules.UpdateOneAsync(s => s.Id == schedule.Id, Builders<AlertNotificationDocument>.Update
            .Set(s => s.Done, false)
            .Set(s => s.NextAt, now + delay)
            .Set(s => s.Repeat, schedule.Repeat == 0 ? 0 : Math.Max(schedule.Repeat, schedule.SentCount + 1))
            .Set(s => s.UpdatedAt, now), cancellationToken: cancellationToken);
        return true;
    }
}

/// <summary>Sends due desktop notifications every second.</summary>
public sealed class AlertNotificationDispatcher(IMongoDatabase database, PushSender sender, ILogger<AlertNotificationDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedules = database.GetCollection<AlertNotificationDocument>(AlertNotificationDocument.CollectionName);
        var alerts = database.GetCollection<IoTAlert>("IoTAlert");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var due = await schedules.Find(s => !s.Done && s.NextAt != null && s.NextAt <= now).Limit(50).ToListAsync(stoppingToken);
                foreach (var schedule in due)
                {
                    var alert = await alerts.Find(a => a.AlertId == schedule.AlertId).FirstOrDefaultAsync(stoppingToken);
                    if (alert is null || alert.IsDeleted || alert.Status != IoTAlertStatus.Open)
                    {
                        // Acknowledged / resolved elsewhere (app, another browser): stop notifying
                        await schedules.UpdateOneAsync(s => s.Id == schedule.Id, Builders<AlertNotificationDocument>.Update
                            .Set(s => s.Done, true).Set(s => s.NextAt, null).Set(s => s.UpdatedAt, now), cancellationToken: stoppingToken);
                        continue;
                    }

                    var number = schedule.SentCount + 1;
                    await sender.SendAsync(schedule.TenantId, subscription => new
                    {
                        type = "iot-alert",
                        alertId = schedule.AlertId,
                        title = schedule.Title,
                        body = schedule.Body,
                        severity = schedule.Severity,
                        sound = schedule.Sound,
                        number,
                        raisedAt = alert.RaisedAt,
                        actionUrl = $"{(subscription.ApiUrl ?? string.Empty).TrimEnd('/')}/api/push/actions",
                        actionToken = schedule.ActionToken,
                        url = $"{(subscription.AppUrl ?? string.Empty).TrimEnd('/')}/tracking-iot/geofence-alerts"
                    }, stoppingToken);

                    var finished = schedule.Repeat > 0 && number >= schedule.Repeat;
                    await schedules.UpdateOneAsync(s => s.Id == schedule.Id, Builders<AlertNotificationDocument>.Update
                        .Set(s => s.SentCount, number)
                        .Set(s => s.Done, finished)
                        .Set(s => s.NextAt, finished ? null : now.AddSeconds(schedule.IntervalSeconds))
                        .Set(s => s.UpdatedAt, now), cancellationToken: stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alert notification dispatch failed");
            }

            await Task.Delay(Tick, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }
}
