using MongoDB.Driver;
using A360.Repository.Repositories;
using SignatureAndStampEntity = A360.Inspection.Domain.Entities.SignatureAndStamp;

namespace A360.Inspection.Repository.Repositories;

public sealed class SignatureAndStampRepository : MongoRepository<SignatureAndStampEntity>, ISignatureAndStampRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "signatures_and_stamps";

    public SignatureAndStampRepository(IMongoDatabase database)
        : base(database.GetCollection<SignatureAndStampEntity>(CollectionName))
    {
    }

    public async Task<SignatureAndStampEntity?> GetBySignatureCodeAsync(string signatureCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(signature => signature.SignatureCode == signatureCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SignatureAndStampEntity>(
                Builders<SignatureAndStampEntity>.IndexKeys.Ascending(signature => signature.SignatureCode),
                new CreateIndexOptions { Name = "ix_signatures_and_stamps_signature_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
