using ChartTypeEntity = A360.MasterDataManagement.Domain.Entities.ChartType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IChartTypeRepository : IMongoRepository<ChartTypeEntity>
{
    Task<IReadOnlyCollection<ChartTypeEntity>> SearchAsync(ChartTypeFilter filter, CancellationToken cancellationToken = default);

    Task<ChartTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ChartTypeFilter(
    string? Search,
    string? Status,
    string? Category,
    string? ClientId);
