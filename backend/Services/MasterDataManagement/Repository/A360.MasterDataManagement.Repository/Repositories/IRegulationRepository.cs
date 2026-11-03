using RegulationEntity = A360.MasterDataManagement.Domain.Entities.Regulation;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IRegulationRepository : IMongoRepository<RegulationEntity>
{
    Task<IReadOnlyCollection<RegulationEntity>> SearchAsync(RegulationFilter filter, CancellationToken cancellationToken = default);

    Task<RegulationEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task UpdateStandardCodeAsync(string fromValue, string toValue, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record RegulationFilter(
    string? Search,
    string? Status,
    string? Country,
    string? Category,
    string? ClientId);
