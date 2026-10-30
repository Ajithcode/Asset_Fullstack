using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class NumberingSequenceRepository
    : MongoRepository<NumberingSequence>, INumberingSequenceRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_numbering_sequences";

    public NumberingSequenceRepository(IMongoDatabase database)
        : base(database.GetCollection<NumberingSequence>(CollectionName))
    {
    }

    public async Task<bool> SequenceCodeExistsAsync(
        string sequenceCode,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NumberingSequence>.Filter;
        var filter = builder.Eq(sequence => sequence.SequenceCode, sequenceCode)
            & builder.Eq(sequence => sequence.IsDeleted, false)
            & builder.Eq(sequence => sequence.TenantId, tenantId);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<NumberingSequence>(
                Builders<NumberingSequence>.IndexKeys.Ascending(sequence => sequence.SequenceId),
                new CreateIndexOptions { Name = "ux_administration_numbering_sequences_sequence_id", Unique = true }),
            new CreateIndexModel<NumberingSequence>(
                Builders<NumberingSequence>.IndexKeys
                    .Ascending(sequence => sequence.TenantId)
                    .Ascending(sequence => sequence.SequenceCode),
                new CreateIndexOptions { Name = "ix_administration_numbering_sequences_tenant_id_sequence_code" }),
            new CreateIndexModel<NumberingSequence>(
                Builders<NumberingSequence>.IndexKeys
                    .Ascending(sequence => sequence.Module)
                    .Ascending(sequence => sequence.RecordType),
                new CreateIndexOptions { Name = "ix_administration_numbering_sequences_module_record_type" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
