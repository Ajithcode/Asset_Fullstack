using MongoDB.Driver;
using A360.Domain.Entities;

namespace A360.Repository.Repositories;

public abstract class MongoRepository<TEntity> : IMongoRepository<TEntity>
    where TEntity : BaseEntity
{
    protected IMongoCollection<TEntity> Collection { get; }

    protected MongoRepository(IMongoCollection<TEntity> collection)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    public async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<TEntity>.Filter.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return null;
        }

        return await Collection
            .Find(entity => entity.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = MongoObjectId.Create();
        }

        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(string id, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!MongoObjectId.IsValid(id))
        {
            return false;
        }

        entity.Id = id;

        var result = await Collection.ReplaceOneAsync(
            document => document.Id == id,
            entity,
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return false;
        }

        var result = await Collection.DeleteOneAsync(entity => entity.Id == id, cancellationToken);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
