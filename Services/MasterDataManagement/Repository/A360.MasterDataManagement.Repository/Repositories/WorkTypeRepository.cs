using System.Text.RegularExpressions;
using WorkTypeEntity = A360.MasterDataManagement.Domain.Entities.WorkType;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class WorkTypeRepository : MongoRepository<WorkTypeEntity>,
    IWorkTypeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_work_types";

    public WorkTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkTypeEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkTypeEntity>> SearchAsync(
        WorkTypeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<WorkTypeEntity>.Filter;
        var filters = new List<FilterDefinition<WorkTypeEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.WorkTypeName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.MaintenanceTypeId))
        {
            filters.Add(builder.Eq(x => x.MaintenanceType!.Id, filter.MaintenanceTypeId));
        }
        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.WorkTypeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkTypeEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<WorkTypeEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithMaintenanceTypeAsync(
        string maintenanceTypeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.MaintenanceType!.Id == maintenanceTypeId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateMaintenanceTypeNameAsync(
        string maintenanceTypeId,
        string maintenanceTypeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.MaintenanceType!.Id == maintenanceTypeId,
            Builders<WorkTypeEntity>.Update.Set(x => x.MaintenanceType!.Name, maintenanceTypeName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<WorkTypeEntity>(
                Builders<WorkTypeEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_work_type_client_code"
                }),
            new CreateIndexModel<WorkTypeEntity>(
                Builders<WorkTypeEntity>.IndexKeys
                    .Ascending(x => x.MaintenanceType!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_work_type_maintenance_type"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
