using ServiceProviderEntity = A360.MasterDataManagement.Domain.Entities.ServiceProvider;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IServiceProviderRepository : IMongoRepository<ServiceProviderEntity>
{
    Task<IReadOnlyCollection<ServiceProviderEntity>> SearchAsync(ServiceProviderFilter filter, CancellationToken cancellationToken = default);

    Task<ServiceProviderEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ServiceProviderFilter(
    string? Search,
    string? Status,
    string? Country,
    string? ServiceType,
    string? ClientId);
