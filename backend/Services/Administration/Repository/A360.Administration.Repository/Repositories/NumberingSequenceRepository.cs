using MongoDB.Bson;
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

    public async Task<NumberingSequence?> GetBySequenceIdAsync(string sequenceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sequenceId))
        {
            return null;
        }

        return await Collection
            .Find(sequence => sequence.SequenceId == sequenceId && !sequence.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<NumberingSequence>> GetAsync(
        string? tenantId,
        string? clientId,
        string? module,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NumberingSequence>.Filter;
        var filter = builder.Eq(sequence => sequence.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(sequence => sequence.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(sequence => sequence.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            filter &= builder.Regex(sequence => sequence.Module, ExactIgnoreCase(module));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(sequence => sequence.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(sequence => sequence.SequenceName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SequenceCodeExistsAsync(
        string sequenceCode,
        string? tenantId,
        string? excludeSequenceId = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NumberingSequence>.Filter;
        var filter = builder.Eq(sequence => sequence.SequenceCode, sequenceCode)
            & builder.Eq(sequence => sequence.IsDeleted, false)
            & builder.Eq(sequence => sequence.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeSequenceId))
        {
            filter &= builder.Ne(sequence => sequence.SequenceId, excludeSequenceId);
        }

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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
