using A360.Repository.Repositories;
using NumberingSequenceEntity = A360.Inspection.Domain.Entities.NumberingSequence;

namespace A360.Inspection.Repository.Repositories;

public interface INumberingSequenceRepository : IMongoRepository<NumberingSequenceEntity>
{
    Task<NumberingSequenceEntity?> GetBySequenceCodeAsync(string sequenceCode, CancellationToken cancellationToken = default);
}
