using CertificationTypeEntity = A360.MasterDataManagement.Domain.Entities.CertificationType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ICertificationTypeRepository : IMongoRepository<CertificationTypeEntity>
{
    Task<IReadOnlyCollection<CertificationTypeEntity>> SearchAsync(CertificationTypeFilter filter, CancellationToken cancellationToken = default);

    Task<CertificationTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task UpdateStandardCodeAsync(string fromValue, string toValue, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record CertificationTypeFilter(
    string? Search,
    string? Status,
    string? Category,
    string? ApplicableTo,
    string? ClientId);
