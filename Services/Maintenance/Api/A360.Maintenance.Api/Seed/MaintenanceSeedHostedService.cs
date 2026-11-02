using MongoDB.Driver;
using A360.Domain.Entities;
using A360.Maintenance.Repository.Repositories;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Seed;

/// <summary>
/// Inserts the sample Maintenance data (matching the module mockups) on startup. Each collection is seeded
/// only when it has no non-deleted documents, so existing data is never touched. Turn it off with
/// "Maintenance:SeedSampleData": false.
/// </summary>
public sealed class MaintenanceSeedHostedService : BackgroundService
{
    private const int MaxAttempts = 5;

    private readonly IMongoDatabase _database;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaintenanceSeedHostedService> _logger;

    public MaintenanceSeedHostedService(IMongoDatabase database, IConfiguration configuration, ILogger<MaintenanceSeedHostedService> logger)
    {
        _database = database;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Maintenance:SeedSampleData", true))
        {
            return;
        }

        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await SeedAsync(stoppingToken);
                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts && !stoppingToken.IsCancellationRequested)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(exception, "Maintenance seed failed on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", attempt, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Maintenance seed failed after {MaxAttempts} attempts. The service will continue running.", MaxAttempts);
                return;
            }
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        await SeedCollectionAsync(WorkOrderRepository.CollectionName, MaintenanceSeedData.WorkOrders(today), cancellationToken);
        await SeedCollectionAsync(IssueReportRepository.CollectionName, MaintenanceSeedData.ServiceRequests(today), cancellationToken);
        await SeedCollectionAsync(PreventiveMaintenanceRepository.CollectionName, MaintenanceSeedData.PmSchedules(today), cancellationToken);
        await SeedCollectionAsync(PredictiveMaintenanceRepository.CollectionName, MaintenanceSeedData.Predictions(today), cancellationToken);
    }

    private async Task SeedCollectionAsync<TEntity>(string collectionName, IReadOnlyList<TEntity> documents, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var collection = _database.GetCollection<TEntity>(collectionName);
        var existing = await collection.CountDocumentsAsync(
            Builders<TEntity>.Filter.Ne(entity => entity.IsDeleted, true),
            cancellationToken: cancellationToken);

        if (existing > 0 || documents.Count == 0)
        {
            return;
        }

        foreach (var document in documents.Where(document => string.IsNullOrWhiteSpace(document.Id)))
        {
            document.Id = MongoObjectId.Create();
        }

        await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        _logger.LogInformation("Seeded {Count} documents into {Collection}.", documents.Count, collectionName);
    }
}
