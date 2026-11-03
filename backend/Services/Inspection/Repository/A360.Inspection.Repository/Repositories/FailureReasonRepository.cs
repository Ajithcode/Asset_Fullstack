using MongoDB.Driver;
using A360.Repository.Repositories;
using FailureReasonEntity = A360.Inspection.Domain.Entities.FailureReason;

namespace A360.Inspection.Repository.Repositories;

public sealed class FailureReasonRepository : MongoRepository<FailureReasonEntity>, IFailureReasonRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "failure_reasons";

    public FailureReasonRepository(IMongoDatabase database)
        : base(database.GetCollection<FailureReasonEntity>(CollectionName))
    {
    }

    public async Task<FailureReasonEntity?> GetByFailureReasonCodeAsync(string failureReasonCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(failureReason => failureReason.FailureReasonCode == failureReasonCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<FailureReasonEntity>(
                Builders<FailureReasonEntity>.IndexKeys.Ascending(failureReason => failureReason.FailureReasonCode),
                new CreateIndexOptions { Name = "ix_failure_reasons_failure_reason_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
