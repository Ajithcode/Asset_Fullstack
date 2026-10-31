using MongoDB.Driver;
using A360.Repository.Repositories;
using InspectionPlanEntity = A360.Inspection.Domain.Entities.InspectionPlan;

namespace A360.Inspection.Repository.Repositories;

public sealed class InspectionPlanRepository : MongoRepository<InspectionPlanEntity>, IInspectionPlanRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "inspection_plans";

    public InspectionPlanRepository(IMongoDatabase database)
        : base(database.GetCollection<InspectionPlanEntity>(CollectionName))
    {
    }

    public async Task<InspectionPlanEntity?> GetByPlanCodeAsync(string planCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(plan => plan.PlanCode == planCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<InspectionPlanEntity>(
                Builders<InspectionPlanEntity>.IndexKeys.Ascending(plan => plan.PlanCode),
                new CreateIndexOptions { Name = "ix_inspection_plans_plan_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
