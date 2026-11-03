using ApiSyncStatusEntity = A360.MasterDataManagement.Domain.Entities.ApiSyncStatus;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IApiSyncStatusRepository : IMongoRepository<ApiSyncStatusEntity>
{
    Task<IReadOnlyCollection<ApiSyncStatusEntity>> SearchAsync(ApiSyncStatusFilter filter, CancellationToken cancellationToken = default);

    Task<ApiSyncStatusEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ApiSyncStatusFilter(
    string? Search,
    string? Status,
    bool? IsSuccess,
    string? ClientId);
