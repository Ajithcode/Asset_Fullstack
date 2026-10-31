
using IssueReportEntity = A360.Maintenance.Domain.Entities.IssueReport;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IIssueReportRepository : IMongoRepository<IssueReportEntity>
{
    Task<IReadOnlyCollection<IssueReportEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
