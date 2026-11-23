using MongoDB.Driver;
using A360.Domain.Entities;
using A360.MasterDataManagement.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Seed;

/// <summary>
/// Inserts the sample master data on startup. The data comes in two groups that reference each other
/// internally: the organization/location hierarchy with employees, and the asset catalog. A group is
/// seeded only when none of its collections has any non-deleted documents, so existing data is never
/// touched and references never point at records that were not seeded together.
/// Turn it off with "MasterDataManagement:SeedSampleData": false.
/// </summary>
public sealed class MasterDataSeedHostedService : BackgroundService
{
    private const int MaxAttempts = 5;

    private readonly IMongoDatabase _database;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MasterDataSeedHostedService> _logger;

    public MasterDataSeedHostedService(IMongoDatabase database, IConfiguration configuration, ILogger<MasterDataSeedHostedService> logger)
    {
        _database = database;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Runs even when sample data is off: it repairs existing records rather than adding new ones
        try
        {
            await BackfillOrganizationCodesAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not give organizations without a code a code.");
        }

        try
        {
            await BackfillCriticalityRangesAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not fill the criticality score ranges.");
        }

        if (!_configuration.GetValue("MasterDataManagement:SeedSampleData", true))
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
                _logger.LogWarning(exception, "Master data seed failed on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", attempt, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Master data seed failed after {MaxAttempts} attempts. The service will continue running.", MaxAttempts);
                return;
            }
        }
    }

    /// <summary>
    /// Organizations saved before the Organization API had a code have none, but the web Organization page keys its
    /// records by code: without one, Save on edit sends POST instead of PUT and fails with 409. Gives each of them
    /// the next free ORG-### code. Records that already have a code are never changed.
    /// </summary>
    private async Task BackfillOrganizationCodesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<Domain.Entities.Organization>(OrganizationRepository.CollectionName);
        var missing = Builders<Domain.Entities.Organization>.Filter.Where(o => o.Code == null || o.Code == "");
        var withoutCode = await collection.Find(missing).SortBy(o => o.CreatedAt).ToListAsync(cancellationToken);
        if (withoutCode.Count == 0)
        {
            return;
        }

        var taken = (await collection.Find(Builders<Domain.Entities.Organization>.Filter.Not(missing)).Project(o => o.Code).ToListAsync(cancellationToken))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .ToHashSet();

        var next = 1;
        foreach (var organization in withoutCode)
        {
            string code;
            do
            {
                code = $"ORG-{next++:D3}";
            }
            while (taken.Contains(code));

            taken.Add(code);
            // Only fills an empty code, so a code set in the meantime is kept
            await collection.UpdateOneAsync(
                Builders<Domain.Entities.Organization>.Filter.Eq(o => o.Id, organization.Id) & missing,
                Builders<Domain.Entities.Organization>.Update.Set(o => o.Code, code),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Organization {OrganizationId} had no code; set it to {Code}.", organization.Id, code);
        }
    }

    /// <summary>
    /// Asset Criticality: one-time bootstrap of the default score ranges (85-100, 65-84, 40-64, 20-39) and display order, and
    /// of a missing Medium or Low level, for a client that has Critical or High levels but NO level with a score range yet
    /// (i.e. data from before Phase 2). Idempotent: as soon as any non-deleted level has a range the client is skipped, so a
    /// level the user edited, renamed or deleted is never re-created or reset on a later start.
    /// </summary>
    private async Task BackfillCriticalityRangesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<Domain.Entities.Criticality>(CriticalityRepository.CollectionName);
        var all = await collection.Find(Builders<Domain.Entities.Criticality>.Filter.Ne(level => level.IsDeleted, true)).ToListAsync(cancellationToken);
        var templates = MasterDataSeedData.Criticalities(DateTime.UtcNow).ToDictionary(level => level.CriticalityName, StringComparer.OrdinalIgnoreCase);

        foreach (var client in all.GroupBy(level => level.ClientId))
        {
            var levels = client.ToList();
            if (!levels.Any(level => IsLevel(level, "Critical") || IsLevel(level, "High")))
            {
                continue;
            }

            // Idempotency guard: defaults are only added while the client has no level with a range at all.
            if (levels.Any(level => level.MinScore is not null || level.MaxScore is not null))
            {
                continue;
            }

            var usedCodes = levels.Select(level => level.Code?.Trim().ToUpperInvariant()).ToHashSet();

            foreach (var (name, range) in Validation.CriticalityRanges.Defaults)
            {
                var existing = levels.FirstOrDefault(level => IsLevel(level, name));
                if (existing is not null)
                {
                    if (existing.MinScore is null && existing.MaxScore is null)
                    {
                        await collection.UpdateOneAsync(
                            Builders<Domain.Entities.Criticality>.Filter.Eq(level => level.Id, existing.Id),
                            Builders<Domain.Entities.Criticality>.Update
                                .Set(level => level.MinScore, range.Min)
                                .Set(level => level.MaxScore, range.Max)
                                .Set(level => level.DisplayOrder, existing.DisplayOrder ?? range.Order),
                            cancellationToken: cancellationToken);
                        _logger.LogInformation("Criticality {Name} ({Code}) got the default score range {Min}-{Max}.", existing.CriticalityName, existing.Code, range.Min, range.Max);
                    }

                    continue;
                }

                if (!templates.TryGetValue(name, out var template))
                {
                    continue;
                }

                var code = template.Code;
                var attempt = 0;
                while (usedCodes.Contains(code.ToUpperInvariant()))
                {
                    code = $"{template.Code}-{++attempt}";
                }

                usedCodes.Add(code.ToUpperInvariant());
                var level = new Domain.Entities.Criticality
                {
                    CriticalityName = template.CriticalityName,
                    Code = code,
                    Color = template.Color,
                    RiskLevel = template.RiskLevel,
                    MaintenancePriority = template.MaintenancePriority,
                    Description = template.Description,
                    MinScore = range.Min,
                    MaxScore = range.Max,
                    DisplayOrder = range.Order,
                    ClientId = client.Key,
                    TenantId = levels.Select(l => l.TenantId).FirstOrDefault(t => !string.IsNullOrEmpty(t)),
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Active",
                    IsDeleted = false
                };
                level.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                await collection.InsertOneAsync(level, cancellationToken: cancellationToken);
                _logger.LogInformation("Added the missing criticality level {Name} ({Code}) with score range {Min}-{Max}.", level.CriticalityName, code, range.Min, range.Max);
            }
        }

        static bool IsLevel(Domain.Entities.Criticality level, string name) =>
            string.Equals(level.CriticalityName?.Trim(), name, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (await AllEmptyAsync(cancellationToken,
                Collection<Domain.Entities.Organization>(OrganizationRepository.CollectionName),
                Collection<Domain.Entities.BusinessUnit>(BusinessUnitRepository.CollectionName),
                Collection<Domain.Entities.Department>(DepartmentRepository.CollectionName),
                Collection<Domain.Entities.Site>(SiteRepository.CollectionName),
                Collection<Domain.Entities.Building>(BuildingRepository.CollectionName),
                Collection<Domain.Entities.Floor>(FloorRepository.CollectionName),
                Collection<Domain.Entities.Zone>(ZoneRepository.CollectionName),
                Collection<Domain.Entities.Location>(LocationRepository.CollectionName),
                Collection<Domain.Entities.Employee>(EmployeeRepository.CollectionName)))
        {
            var set = MasterDataSeedData.Organization(now);
            await InsertAsync(OrganizationRepository.CollectionName, set.Organizations, cancellationToken);
            await InsertAsync(BusinessUnitRepository.CollectionName, set.BusinessUnits, cancellationToken);
            await InsertAsync(DepartmentRepository.CollectionName, set.Departments, cancellationToken);
            await InsertAsync(EmployeeRepository.CollectionName, set.Employees, cancellationToken);
            await InsertAsync(SiteRepository.CollectionName, set.Sites, cancellationToken);
            await InsertAsync(BuildingRepository.CollectionName, set.Buildings, cancellationToken);
            await InsertAsync(FloorRepository.CollectionName, set.Floors, cancellationToken);
            await InsertAsync(ZoneRepository.CollectionName, set.Zones, cancellationToken);
            await InsertAsync(LocationRepository.CollectionName, set.Locations, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Organization/location master data already exists; skipping that sample data.");
        }

        if (await AllEmptyAsync(cancellationToken,
                Collection<Domain.Entities.AssetCategory>(AssetCategoryRepository.CollectionName),
                Collection<Domain.Entities.AssetSubCategory>(AssetSubCategoryRepository.CollectionName),
                Collection<Domain.Entities.AssetType>(AssetTypeRepository.CollectionName)))
        {
            var set = MasterDataSeedData.AssetCatalog(now);
            await InsertAsync(AssetCategoryRepository.CollectionName, set.Categories, cancellationToken);
            await InsertAsync(AssetSubCategoryRepository.CollectionName, set.SubCategories, cancellationToken);
            await InsertAsync(AssetTypeRepository.CollectionName, set.AssetTypes, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Asset catalog master data already exists; skipping that sample data.");
        }

        await SeedListAsync(CriticalityRepository.CollectionName, () => MasterDataSeedData.Criticalities(now), cancellationToken);
        await SeedListAsync(ConditionRepository.CollectionName, () => MasterDataSeedData.Conditions(now), cancellationToken);
        await SeedListAsync(UomRepository.CollectionName, () => MasterDataSeedData.Uoms(now), cancellationToken);
        await SeedListAsync(ShiftRepository.CollectionName, () => MasterDataSeedData.Shifts(now), cancellationToken);

        // Vendors and models link to asset types, so they are looked up from whatever is in the database now.
        if (await AllEmptyAsync(cancellationToken,
                Collection<Domain.Entities.Manufacturer>(ManufacturerRepository.CollectionName),
                Collection<Domain.Entities.AssetModel>(AssetModelRepository.CollectionName),
                Collection<Domain.Entities.Supplier>(SupplierRepository.CollectionName),
                Collection<Domain.Entities.ServiceProvider>(ServiceProviderRepository.CollectionName),
                Collection<Domain.Entities.AmcVendor>(AmcVendorRepository.CollectionName)))
        {
            var assetTypes = (await ActiveSeedClientDocumentsAsync<Domain.Entities.AssetType>(AssetTypeRepository.CollectionName, cancellationToken))
                .GroupBy(type => type.Code)
                .ToDictionary(group => group.Key, group =>
                {
                    var type = group.First();
                    return new MasterDataSeedData.AssetTypeLookup(type.Id, type.AssetTypeName, type.Category);
                });

            var set = MasterDataSeedData.Vendors(now, assetTypes);
            await InsertAsync(ManufacturerRepository.CollectionName, set.Manufacturers, cancellationToken);
            await InsertAsync(AssetModelRepository.CollectionName, set.AssetModels, cancellationToken);
            await InsertAsync(SupplierRepository.CollectionName, set.Suppliers, cancellationToken);
            await InsertAsync(ServiceProviderRepository.CollectionName, set.ServiceProviders, cancellationToken);
            await InsertAsync(AmcVendorRepository.CollectionName, set.AmcVendors, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Vendor / asset model master data already exists; skipping that sample data.");
        }

        if (await AllEmptyAsync(cancellationToken,
                Collection<Domain.Entities.Team>(TeamRepository.CollectionName),
                Collection<Domain.Entities.Technician>(TechnicianRepository.CollectionName)))
        {
            var departments = (await ActiveSeedClientDocumentsAsync<Domain.Entities.Department>(DepartmentRepository.CollectionName, cancellationToken))
                .GroupBy(department => department.Code)
                .ToDictionary(
                    group => group.Key,
                    group => new Domain.ValueObjects.MasterDataReference { Id = group.First().Id, Name = group.First().DepartmentName });

            var set = MasterDataSeedData.Workforce(now, departments);
            await InsertAsync(TeamRepository.CollectionName, set.Teams, cancellationToken);
            await InsertAsync(TechnicianRepository.CollectionName, set.Technicians, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Team / technician master data already exists; skipping that sample data.");
        }
    }

    private async Task SeedListAsync<TEntity>(string collectionName, Func<IReadOnlyList<TEntity>> documents, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        if (await AllEmptyAsync(cancellationToken, Collection<TEntity>(collectionName)))
        {
            await InsertAsync(collectionName, documents(), cancellationToken);
        }
        else
        {
            _logger.LogInformation("{Collection} already has data; skipping its sample data.", collectionName);
        }
    }

    private async Task<List<TEntity>> ActiveSeedClientDocumentsAsync<TEntity>(string collectionName, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var filter = Builders<TEntity>.Filter.Ne(entity => entity.IsDeleted, true)
            & Builders<TEntity>.Filter.Eq(entity => entity.ClientId, MasterDataSeedData.ClientId);

        return await _database.GetCollection<TEntity>(collectionName).Find(filter).ToListAsync(cancellationToken);
    }

    private Func<CancellationToken, Task<long>> Collection<TEntity>(string collectionName)
        where TEntity : BaseEntity
    {
        return cancellationToken => _database.GetCollection<TEntity>(collectionName).CountDocumentsAsync(
            Builders<TEntity>.Filter.Ne(entity => entity.IsDeleted, true),
            cancellationToken: cancellationToken);
    }

    private static async Task<bool> AllEmptyAsync(CancellationToken cancellationToken, params Func<CancellationToken, Task<long>>[] counters)
    {
        foreach (var count in counters)
        {
            if (await count(cancellationToken) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private async Task InsertAsync<TEntity>(string collectionName, IReadOnlyList<TEntity> documents, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        if (documents.Count == 0)
        {
            return;
        }

        await _database.GetCollection<TEntity>(collectionName).InsertManyAsync(documents, cancellationToken: cancellationToken);
        _logger.LogInformation("Seeded {Count} documents into {Collection}.", documents.Count, collectionName);
    }
}
