using A360.Domain.Entities;

namespace A360.Repository.Repositories;

public interface IMongoRepository<TEntity>
    where TEntity : BaseEntity
{
    Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(string id, TEntity entity, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
