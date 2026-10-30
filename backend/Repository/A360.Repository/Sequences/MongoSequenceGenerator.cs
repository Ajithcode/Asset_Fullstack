using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Repository.Sequences;

public sealed class MongoSequenceGenerator : ISequenceGenerator
{
    public const string CollectionName = "counters";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoSequenceGenerator(IMongoDatabase database)
    {
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task<long> GetNextValueAsync(string sequenceName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", sequenceName);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1L);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result["seq"].ToInt64();
    }
}
