using System.Text.RegularExpressions;
using PermissionEntity = A360.MasterDataManagement.Domain.Entities.Permission;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class PermissionRepository : MongoRepository<PermissionEntity>,
    IPermissionRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_permissions";

    public PermissionRepository(IMongoDatabase database)
        : base(database.GetCollection<PermissionEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PermissionEntity>> SearchAsync(
        PermissionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<PermissionEntity>.Filter;
        var filters = new List<FilterDefinition<PermissionEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.PermissionName, pattern),
                builder.Regex(x => x.Code, pattern),
                builder.Regex(x => x.Module!.Name, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ModuleId))
        {
            filters.Add(builder.Eq(x => x.Module!.Id, filter.ModuleId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filters.Add(builder.Regex(x => x.Type, ExactIgnoreCase(filter.Type)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.PermissionName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PermissionEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<PermissionEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithModuleAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Module!.Id == moduleId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateModuleNameAsync(
        string moduleId,
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Module!.Id == moduleId,
            Builders<PermissionEntity>.Update.Set(x => x.Module!.Name, moduleName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PermissionEntity>(
                Builders<PermissionEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_permission_client_code"
                }),
            new CreateIndexModel<PermissionEntity>(
                Builders<PermissionEntity>.IndexKeys
                    .Ascending(x => x.Module!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_permission_module"
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
