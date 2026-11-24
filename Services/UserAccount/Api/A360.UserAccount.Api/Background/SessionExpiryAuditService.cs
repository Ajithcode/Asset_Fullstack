using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.UserAccount.Repository.Repositories;
using RefreshTokenEntity = A360.UserAccount.Domain.Entities.RefreshToken;

namespace A360.UserAccount.Api.Background;

/// <summary>
/// Writes a "Session expired" audit event for every session that ended without sign-out: a refresh token that expired
/// without being refreshed or revoked (the browser was closed or the session left unused until it ran out). Runs every
/// few minutes; each token is marked when logged, so the event is written once. Signed-out, refreshed and force-ended
/// sessions are revoked tokens and are not included.
/// </summary>
public sealed class SessionExpiryAuditService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SessionExpiryAuditService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let index creation (TTL retention) run first
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await LogExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Could not write the session expired audit events; retrying in {Minutes} minutes.", Interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task LogExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var eventLogger = scope.ServiceProvider.GetRequiredService<IEventLogger>();

        IReadOnlyList<RefreshTokenEntity> batch;
        var logged = 0;
        do
        {
            batch = await tokens.GetEndedUnloggedSessionsAsync(DateTime.UtcNow, BatchSize, cancellationToken);
            foreach (var token in batch)
            {
                // Mark first: with several instances only one writes the event
                if (!await tokens.MarkSessionEndLoggedAsync(token.Id, cancellationToken))
                {
                    continue;
                }

                var user = await users.GetByIdAsync(token.UserRefId, cancellationToken);
                var actor = user is null
                    ? new EventActor(token.UserRefId, "Unknown user", token.ClientId, token.TenantId)
                    : new EventActor(user.Id, user.UserName, user.ClientId, user.TenantId);
                var started = token.CreatedAt is { } createdAt ? $" Last renewed {createdAt:yyyy-MM-dd HH:mm} UTC." : string.Empty;
                var details = $"Session ended without sign-out: unused until it expired at {token.ExpiresAt:yyyy-MM-dd HH:mm} UTC.{started}";

                await eventLogger.LogDetailedAsync("User", actor.RefId ?? string.Empty, actor.UserName, EventAction.SessionExpired, details, actor, cancellationToken);
                logged++;
            }
        }
        while (batch.Count == BatchSize && !cancellationToken.IsCancellationRequested);

        if (logged > 0)
        {
            logger.LogInformation("System Audit Log: {Count} expired sessions logged.", logged);
        }
    }
}
