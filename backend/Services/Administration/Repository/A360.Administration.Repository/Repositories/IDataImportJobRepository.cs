using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IDataImportJobRepository : IMongoRepository<DataImportJob>
{
    Task<DataImportJob?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DataImportJob>> GetAsync(
        string? tenantId,
        string? clientId,
        string? dataObject,
        string? status,
        CancellationToken cancellationToken = default);
}
