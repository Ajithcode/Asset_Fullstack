using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class FloorPlanRepository : MongoRepository<FloorPlan>, IFloorPlanRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_floor_plans";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public FloorPlanRepository(IMongoDatabase database)
        : base(database.GetCollection<FloorPlan>(CollectionName))
    {
    }

    public async Task<FloorPlan?> GetByFloorPlanIdAsync(string floorPlanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(floorPlanId))
        {
            return null;
        }

        return await Collection
            .Find(plan => plan.FloorPlanId == floorPlanId && !plan.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> NameVersionExistsAsync(
        string name,
        string version,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                plan => plan.Name == name && plan.Version == version && plan.TenantId == tenantId && !plan.IsDeleted,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<FloorPlan>(
                Builders<FloorPlan>.IndexKeys.Ascending(plan => plan.FloorPlanId),
                new CreateIndexOptions { Name = "ux_administration_floor_plans_floor_plan_id", Unique = true }),
            new CreateIndexModel<FloorPlan>(
                Builders<FloorPlan>.IndexKeys
                    .Ascending(plan => plan.TenantId)
                    .Ascending(plan => plan.Name)
                    .Ascending(plan => plan.Version),
                new CreateIndexOptions { Name = "ix_administration_floor_plans_tenant_id_name_version", Collation = CaseInsensitive }),
            new CreateIndexModel<FloorPlan>(
                Builders<FloorPlan>.IndexKeys.Ascending(plan => plan.LocationId),
                new CreateIndexOptions { Name = "ix_administration_floor_plans_location_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
