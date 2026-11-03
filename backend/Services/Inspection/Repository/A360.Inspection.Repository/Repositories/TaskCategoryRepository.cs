using MongoDB.Driver;
using A360.Repository.Repositories;
using TaskCategoryEntity = A360.Inspection.Domain.Entities.TaskCategory;

namespace A360.Inspection.Repository.Repositories;

public sealed class TaskCategoryRepository : MongoRepository<TaskCategoryEntity>, ITaskCategoryRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "task_categories";

    public TaskCategoryRepository(IMongoDatabase database)
        : base(database.GetCollection<TaskCategoryEntity>(CollectionName))
    {
    }

    public async Task<TaskCategoryEntity?> GetByCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(taskCategory => taskCategory.CategoryCode == categoryCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TaskCategoryEntity>(
                Builders<TaskCategoryEntity>.IndexKeys.Ascending(taskCategory => taskCategory.CategoryCode),
                new CreateIndexOptions { Name = "ix_task_categories_category_code", Unique = true }),
            new CreateIndexModel<TaskCategoryEntity>(
                Builders<TaskCategoryEntity>.IndexKeys.Ascending(taskCategory => taskCategory.AssetId),
                new CreateIndexOptions { Name = "ix_task_categories_asset_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
