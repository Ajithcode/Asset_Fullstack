using MongoDB.Driver;
using WorkerService.Models;

namespace WorkerService.Services;

public class MongoRepository
{
    private readonly IMongoCollection<DeviceMaster> _devices;
    private readonly IMongoCollection<SensorHistory> _history;

    public MongoRepository(IMongoDatabase db)
    {
        _devices = db.GetCollection<DeviceMaster>("device");

        _history = db.GetCollection<SensorHistory>("myDevice_with_rawdata");

        _history.Indexes.CreateOne(new CreateIndexModel<SensorHistory>(
            Builders<SensorHistory>.IndexKeys.Ascending(x => x.stream_entry_id),
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<DeviceMaster> GetDevice(string uniqueId)
    {
        return await _devices
            .Find(x => x.unique_id == uniqueId)
            .FirstOrDefaultAsync();
    }

    public async Task Save(SensorHistory sensor)
    {
        // Upsert keyed on the Redis stream entry ID so reprocessing the same
        // entry (after a crash/restart between the Mongo write and the Redis
        // ack, or a consumer race under StreamAutoClaim) overwrites the same
        // document instead of inserting a duplicate.
        await _history.ReplaceOneAsync(
            x => x.stream_entry_id == sensor.stream_entry_id,
            sensor,
            new ReplaceOptions { IsUpsert = true });
    }
}