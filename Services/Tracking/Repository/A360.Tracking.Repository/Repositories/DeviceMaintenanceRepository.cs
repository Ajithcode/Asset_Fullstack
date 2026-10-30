using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class DeviceMaintenanceRepository
    : MongoRepository<DeviceMaintenance>, IDeviceMaintenanceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "DeviceMaintenance";

    // Compares text ignoring case, for maintenance id lookups and duplicate jobs.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public DeviceMaintenanceRepository(IMongoDatabase database)
        : base(database.GetCollection<DeviceMaintenance>(CollectionName))
    {
    }

    public async Task<DeviceMaintenance?> GetByMaintenanceIdAsync(string maintenanceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(job => job.MaintenanceId == maintenanceId && !job.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string deviceId, string maintenanceType, DateTime dueDate, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                job => job.DeviceId == deviceId && job.MaintenanceType == maintenanceType && job.DueDate == dueDate && !job.IsDeleted,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, DeviceMaintenance>> GetLatestByDeviceIdsAsync(
        IReadOnlyCollection<string> deviceIds, CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
        {
            return new Dictionary<string, DeviceMaintenance>(StringComparer.OrdinalIgnoreCase);
        }

        var jobs = await Collection
            .Find(
                Builders<DeviceMaintenance>.Filter.In(job => job.DeviceId, deviceIds)
                & Builders<DeviceMaintenance>.Filter.Eq(job => job.IsDeleted, false),
                new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);

        return jobs
            .GroupBy(job => job.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DeviceMaintenance>(
                Builders<DeviceMaintenance>.IndexKeys.Ascending(job => job.MaintenanceId),
                new CreateIndexOptions { Name = "ix_device_maintenance_maintenance_id", Unique = true, Collation = CaseInsensitive }),
            new CreateIndexModel<DeviceMaintenance>(
                Builders<DeviceMaintenance>.IndexKeys
                    .Ascending(job => job.DeviceId)
                    .Ascending(job => job.DueDate),
                new CreateIndexOptions { Name = "ix_device_maintenance_device_id_due_date", Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
