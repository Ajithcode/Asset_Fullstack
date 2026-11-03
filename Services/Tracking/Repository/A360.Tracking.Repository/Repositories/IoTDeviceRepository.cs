using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public sealed class IoTDeviceRepository
    : MongoRepository<IoTDevice>, IIoTDeviceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "IoTDevice";

    // Compares text ignoring case, for device id lookups and uniqueness.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public IoTDeviceRepository(IMongoDatabase database)
        : base(database.GetCollection<IoTDevice>(CollectionName))
    {
    }

    public async Task<IoTDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId && !device.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(device => device.DeviceId == deviceId, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> SerialNumberExistsAsync(string serialNumber, string? exceptId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<IoTDevice>.Filter.Eq(device => device.SerialNumber, serialNumber);
        if (!string.IsNullOrEmpty(exceptId))
        {
            filter &= Builders<IoTDevice>.Filter.Ne(device => device.Id, exceptId);
        }

        return await Collection
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IoTDevice>> SearchAsync(string? search, int limit, CancellationToken cancellationToken = default)
    {
        var filter = Builders<IoTDevice>.Filter.Where(device => !device.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
            filter &= Builders<IoTDevice>.Filter.Or(
                Builders<IoTDevice>.Filter.Regex(device => device.DeviceId, pattern),
                Builders<IoTDevice>.Filter.Regex(device => device.DeviceName, pattern),
                Builders<IoTDevice>.Filter.Regex(device => device.SerialNumber, pattern),
                Builders<IoTDevice>.Filter.Regex(device => device.AssetCode, pattern),
                Builders<IoTDevice>.Filter.Regex(device => device.AssetName, pattern));
        }

        return await Collection
            .Find(filter)
            .SortBy(device => device.DeviceId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IoTDevice>> GetGatewaysAsync(string? siteId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<IoTDevice>.Filter.Where(device => device.DeviceType == IoTDeviceType.Gateway && !device.IsDeleted);
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            filter &= Builders<IoTDevice>.Filter.Eq(device => device.SiteId, siteId.Trim());
        }

        return await Collection
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .SortBy(device => device.DeviceId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<IoTDevice>(
                Builders<IoTDevice>.IndexKeys.Ascending(device => device.DeviceId),
                new CreateIndexOptions { Name = "ix_iot_device_device_id", Unique = true, Collation = CaseInsensitive }),
            // Older devices have no serial number, so uniqueness is enforced in the API rather than by a unique index.
            new CreateIndexModel<IoTDevice>(
                Builders<IoTDevice>.IndexKeys.Ascending(device => device.SerialNumber),
                new CreateIndexOptions { Name = "ix_iot_device_serial_number", Collation = CaseInsensitive }),
            new CreateIndexModel<IoTDevice>(
                Builders<IoTDevice>.IndexKeys.Ascending(device => device.SiteId),
                new CreateIndexOptions { Name = "ix_iot_device_site_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
