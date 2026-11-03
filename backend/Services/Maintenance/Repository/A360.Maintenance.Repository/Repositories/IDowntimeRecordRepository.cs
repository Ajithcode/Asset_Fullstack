
using DowntimeRecordEntity = A360.Maintenance.Domain.Entities.DowntimeRecord;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public interface IDowntimeRecordRepository : IMongoRepository<DowntimeRecordEntity>
{
    Task<IReadOnlyCollection<DowntimeRecordEntity>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
