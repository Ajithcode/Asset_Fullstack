using MongoDB.Bson;
using MongoDB.Driver;
using A360.Domain.Entities;
using A360.Inspection.Api.Endpoints;
using A360.Inspection.Domain.Entities;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;

namespace A360.Inspection.Api.Seed;

/// <summary>
/// Inserts the sample Inspection data (matching the module mockups) on startup. Each collection is seeded
/// only when it has no non-deleted documents, so existing data is never touched. After seeding, the code
/// counters are raised past the seeded codes so the next generated reference does not collide.
/// </summary>
public sealed class InspectionSeedHostedService : BackgroundService
{
    private const int MaxAttempts = 5;

    private readonly IMongoDatabase _database;
    private readonly ILogger<InspectionSeedHostedService> _logger;

    public InspectionSeedHostedService(IMongoDatabase database, ILogger<InspectionSeedHostedService> logger)
    {
        _database = database;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                _logger.LogWarning(exception, "Inspection seed failed on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", attempt, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Inspection seed failed after {MaxAttempts} attempts. The service will continue running.", MaxAttempts);
                return;
            }
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedYear = InspectionSeedData.SeedYear;

        await SeedCollectionAsync(InspectionTypeRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.InspectionTypes()),
            [(InspectionCodes.InspectionTypeSequence, 8)], cancellationToken);
        await SeedCollectionAsync(TaskCategoryRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.TaskCategories()),
            [(InspectionCodes.TaskCategorySequence, 6)], cancellationToken);
        await SeedCollectionAsync(InspectionTaskRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.InspectionTasks()),
            [(InspectionCodes.InspectionTaskSequence, 12)], cancellationToken);
        await SeedCollectionAsync(ChecklistRepository.CollectionName, BuildChecklistsAsync,
            [(InspectionCodes.ChecklistSequence, 5)], cancellationToken);
        await SeedCollectionAsync(InspectionPlanRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.InspectionPlans()),
            [(InspectionCodes.InspectionPlanSequence, 6)], cancellationToken);
        await SeedCollectionAsync(InspectionRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.Inspections()),
            [(InspectionCodes.YearlySequenceName(InspectionCodes.InspectionSequencePrefix, seedYear), 157)], cancellationToken);
        await SeedCollectionAsync(InspectionFindingRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.Findings()),
            [(InspectionCodes.YearlySequenceName(InspectionCodes.FindingSequencePrefix, seedYear), 87)], cancellationToken);
        await SeedCollectionAsync(CalibrationInstrumentRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.CalibrationInstruments()),
            [], cancellationToken);
        await SeedCollectionAsync(SignatureAndStampRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.SignaturesAndStamps()),
            [(InspectionCodes.SignatureSequence, 3), (InspectionCodes.StampSequence, 1)], cancellationToken);
        await SeedCollectionAsync(NotificationTemplateRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.NotificationTemplates()),
            [(InspectionCodes.NotificationTemplateSequence, 6)], cancellationToken);
        await SeedCollectionAsync(ReportTemplateRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.ReportTemplates()),
            [(InspectionCodes.ReportTemplateSequence, 3)], cancellationToken);
        await SeedCollectionAsync(NumberingSequenceRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.NumberingSequences()),
            [(InspectionCodes.NumberingSequenceSequence, 5)], cancellationToken);
        await SeedCollectionAsync(HolidayAndWorkingCalendarRepository.CollectionName, _ => Task.FromResult(InspectionSeedData.WorkingCalendars()),
            [(InspectionCodes.CalendarSequence, 1)], cancellationToken);
    }

    /// <summary>Checklists link to the inspection type and task records by id, so look those up first.</summary>
    private async Task<IReadOnlyList<Checklist>> BuildChecklistsAsync(CancellationToken cancellationToken)
    {
        var types = await _database.GetCollection<InspectionType>(InspectionTypeRepository.CollectionName)
            .Find(Builders<InspectionType>.Filter.Empty).ToListAsync(cancellationToken);
        var tasks = await _database.GetCollection<InspectionTask>(InspectionTaskRepository.CollectionName)
            .Find(Builders<InspectionTask>.Filter.Empty).ToListAsync(cancellationToken);

        var typeIds = types
            .Where(type => !string.IsNullOrEmpty(type.InspectionTypeName))
            .GroupBy(type => type.InspectionTypeName)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var taskIds = tasks
            .Where(task => !string.IsNullOrEmpty(task.TaskCode))
            .GroupBy(task => task.TaskCode)
            .ToDictionary(group => group.Key, group => group.First().Id);

        return InspectionSeedData.Checklists(typeIds, taskIds);
    }

    private async Task SeedCollectionAsync<TEntity>(
        string collectionName,
        Func<CancellationToken, Task<IReadOnlyList<TEntity>>> build,
        (string SequenceName, long Value)[] counters,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var collection = _database.GetCollection<TEntity>(collectionName);
        var existing = await collection.CountDocumentsAsync(
            Builders<TEntity>.Filter.Ne(entity => entity.IsDeleted, true),
            cancellationToken: cancellationToken);

        if (existing > 0)
        {
            return;
        }

        var documents = await build(cancellationToken);
        foreach (var document in documents.Where(document => string.IsNullOrWhiteSpace(document.Id)))
        {
            document.Id = MongoObjectId.Create();
        }

        if (documents.Count > 0)
        {
            await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }

        var counterCollection = _database.GetCollection<BsonDocument>(MongoSequenceGenerator.CollectionName);
        foreach (var (sequenceName, value) in counters)
        {
            await counterCollection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", sequenceName),
                Builders<BsonDocument>.Update.Max("seq", value),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} documents into {Collection}.", documents.Count, collectionName);
    }
}
