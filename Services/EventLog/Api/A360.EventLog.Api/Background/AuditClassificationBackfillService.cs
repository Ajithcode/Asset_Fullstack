using A360.Repository.Repositories;

namespace A360.EventLog.Api.Background;

/// <summary>
/// On startup, gives events written before the System Audit Log existed (or classified with older rules) their category,
/// severity, outcome and module, in batches, so they show up under the right tab. New events are classified when written.
/// </summary>
public sealed class AuditClassificationBackfillService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AuditClassificationBackfillService> logger) : BackgroundService
{
    private const int BatchSize = 1_000;
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IEventLogRepository>();

                var total = 0;
                int updated;
                while ((updated = await repository.ClassifyOutdatedAsync(BatchSize, stoppingToken)) > 0)
                {
                    total += updated;
                }

                if (total > 0)
                {
                    logger.LogInformation("System Audit Log: classified {Count} older events.", total);
                }

                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts && !stoppingToken.IsCancellationRequested)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(exception, "System Audit Log classification failed on attempt {Attempt}. Retrying in {Seconds} seconds.", attempt, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
            }
        }
    }
}
