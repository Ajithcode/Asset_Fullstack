using MongoDB.Driver;
using A360.Repository.Repositories;
using NumberingSequenceEntity = A360.Inspection.Domain.Entities.NumberingSequence;

namespace A360.Inspection.Repository.Repositories;

public sealed class NumberingSequenceRepository : MongoRepository<NumberingSequenceEntity>, INumberingSequenceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "numbering_sequences";

    public NumberingSequenceRepository(IMongoDatabase database)
        : base(database.GetCollection<NumberingSequenceEntity>(CollectionName))
    {
    }

    public async Task<NumberingSequenceEntity?> GetBySequenceCodeAsync(string sequenceCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(sequence => sequence.SequenceCode == sequenceCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<NumberingSequenceEntity>(
                Builders<NumberingSequenceEntity>.IndexKeys.Ascending(sequence => sequence.SequenceCode),
                new CreateIndexOptions { Name = "ix_numbering_sequences_sequence_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
