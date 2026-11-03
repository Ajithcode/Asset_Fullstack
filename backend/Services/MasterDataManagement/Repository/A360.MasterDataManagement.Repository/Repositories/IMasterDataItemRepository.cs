using MasterDataItemEntity = A360.MasterDataManagement.Domain.Entities.MasterDataItem;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IMasterDataItemRepository : IMongoRepository<MasterDataItemEntity>
{
    Task<IReadOnlyCollection<MasterDataItemEntity>> GetByMasterTypeAsync(string masterType, CancellationToken cancellationToken = default);
}
