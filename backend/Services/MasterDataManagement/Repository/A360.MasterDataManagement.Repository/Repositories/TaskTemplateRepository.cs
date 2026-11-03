using System.Text.RegularExpressions;
using TaskTemplateEntity = A360.MasterDataManagement.Domain.Entities.TaskTemplate;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class TaskTemplateRepository : MongoRepository<TaskTemplateEntity>,
    ITaskTemplateRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_task_templates";

    public TaskTemplateRepository(IMongoDatabase database)
        : base(database.GetCollection<TaskTemplateEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<TaskTemplateEntity>> SearchAsync(
        TaskTemplateFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TaskTemplateEntity>.Filter;
        var filters = new List<FilterDefinition<TaskTemplateEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.TemplateName, pattern),
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
            .SortBy(x => x.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskTemplateEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TaskTemplateEntity>.Filter;
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
            Builders<TaskTemplateEntity>.Update.Set(x => x.MaintenanceType!.Name, maintenanceTypeName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TaskTemplateEntity>(
                Builders<TaskTemplateEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_task_template_client_code"
                }),
            new CreateIndexModel<TaskTemplateEntity>(
                Builders<TaskTemplateEntity>.IndexKeys
                    .Ascending(x => x.MaintenanceType!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_task_template_maintenance_type"
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
