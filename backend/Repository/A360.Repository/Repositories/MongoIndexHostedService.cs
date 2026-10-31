using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace A360.Repository.Repositories;

public sealed class MongoIndexHostedService : BackgroundService
{
    private const int MaxAttempts = 5;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MongoIndexHostedService> _logger;

    public MongoIndexHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MongoIndexHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await CreateIndexesAsync(stoppingToken);
                _logger.LogInformation("MongoDB index creation completed.");
                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts && !stoppingToken.IsCancellationRequested)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    exception,
                    "MongoDB index creation failed on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.",
                    attempt,
                    retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "MongoDB index creation failed after {MaxAttempts} attempts. The service will continue running.",
                    MaxAttempts);
                return;
            }
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var indexConfigurators = scope.ServiceProvider.GetServices<IMongoIndexConfigurator>();

        foreach (var indexConfigurator in indexConfigurators)
        {
            await indexConfigurator.CreateIndexesAsync(cancellationToken);
        }
    }
}
