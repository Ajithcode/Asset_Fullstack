using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetBulkAssignmentRepository : IMongoRepository<AssetBulkAssignment>
{
}
