using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class FloorPlanRepository : MongoRepository<FloorPlan>, IFloorPlanRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_floor_plans";

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

    public async Task<IReadOnlyCollection<FloorPlan>> GetAsync(
        string? locationId,
        string? status,
        string? planType,
        string? search,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<FloorPlan>.Filter;
        var filter = builder.Eq(plan => plan.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            filter &= builder.Eq(plan => plan.LocationId, locationId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(plan => plan.Status, ExactIgnoreCase(status));
        }

        if (!string.IsNullOrWhiteSpace(planType))
        {
            filter &= builder.Regex(plan => plan.PlanType, ExactIgnoreCase(planType));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
            filter &= builder.Regex(plan => plan.Name, pattern)
                | builder.Regex(plan => plan.Description, pattern)
                | builder.Regex(plan => plan.FileName, pattern)
                | builder.Regex(plan => plan.LocationPath, pattern);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(plan => plan.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(plan => plan.TenantId, tenantId);
        }

        var plans = await Collection.Find(filter).ToListAsync(cancellationToken);

        // Newest change first; plans never edited count from their creation
        return plans
            .OrderByDescending(plan => plan.UpdatedAt ?? plan.CreatedAt)
            .ToList();
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<FloorPlan>(
                Builders<FloorPlan>.IndexKeys.Ascending(plan => plan.FloorPlanId),
                new CreateIndexOptions { Name = "ux_maps_floor_plans_floor_plan_id", Unique = true }),
            new CreateIndexModel<FloorPlan>(
                Builders<FloorPlan>.IndexKeys
                    .Ascending(plan => plan.TenantId)
                    .Ascending(plan => plan.LocationId),
                new CreateIndexOptions { Name = "ix_maps_floor_plans_tenant_id_location_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
