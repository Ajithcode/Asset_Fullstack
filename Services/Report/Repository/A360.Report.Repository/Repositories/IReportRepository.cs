using A360.Repository.Repositories;
using ReportEntity = A360.Report.Domain.Entities.Report;

namespace A360.Report.Repository.Repositories;

public interface IReportRepository : IMongoRepository<ReportEntity>
{
    Task<ReportEntity?> GetByReportIdAsync(string reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportEntity>> GetAsync(string? tenantId, string? clientId, CancellationToken cancellationToken = default);
}
