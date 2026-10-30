using A360.Repository.Repositories;
using SignatureAndStampEntity = A360.Inspection.Domain.Entities.SignatureAndStamp;

namespace A360.Inspection.Repository.Repositories;

public interface ISignatureAndStampRepository : IMongoRepository<SignatureAndStampEntity>
{
    Task<SignatureAndStampEntity?> GetBySignatureCodeAsync(string signatureCode, CancellationToken cancellationToken = default);
}
