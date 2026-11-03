using A360.Repository.Repositories;
using SeverityEntity = A360.Inspection.Domain.Entities.Severity;

namespace A360.Inspection.Repository.Repositories;

public interface ISeverityRepository : IMongoRepository<SeverityEntity>
{
    Task<SeverityEntity?> GetBySeverityCodeAsync(string severityCode, CancellationToken cancellationToken = default);
}
