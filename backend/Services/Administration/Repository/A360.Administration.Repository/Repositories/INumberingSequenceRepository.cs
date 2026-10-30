using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface INumberingSequenceRepository : IMongoRepository<NumberingSequence>
{
    Task<NumberingSequence?> GetBySequenceIdAsync(string sequenceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NumberingSequence>> GetAsync(
        string? tenantId,
        string? clientId,
        string? module,
        string? status,
        CancellationToken cancellationToken = default);

    // excludeSequenceId is the sequence being updated, so it does not clash with itself.
    Task<bool> SequenceCodeExistsAsync(
        string sequenceCode,
        string? tenantId,
        string? excludeSequenceId = null,
        CancellationToken cancellationToken = default);
}
