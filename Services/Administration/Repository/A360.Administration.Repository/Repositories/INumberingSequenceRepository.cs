using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface INumberingSequenceRepository : IMongoRepository<NumberingSequence>
{
    Task<bool> SequenceCodeExistsAsync(string sequenceCode, string? tenantId, CancellationToken cancellationToken = default);
}
