using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IChecklistItemRepository : IMongoRepository<ChecklistItem>
{
}
