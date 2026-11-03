using System.Text.RegularExpressions;
using BuildingEntity = A360.MasterDataManagement.Domain.Entities.Building;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class BuildingRepository : MongoRepository<BuildingEntity>,
    IBuildingRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_buildings";

    public BuildingRepository(IMongoDatabase database)
        : base(database.GetCollection<BuildingEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<BuildingEntity>> SearchAsync(
        BuildingFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BuildingEntity>.Filter;
        var filters = new List<FilterDefinition<BuildingEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.Name, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentLevel))
        {
            filters.Add(builder.Regex(x => x.ParentLevel, ExactIgnoreCase(filter.ParentLevel)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SiteId))
        {
            filters.Add(builder.Eq(x => x.Site!.Id, filter.SiteId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<BuildingEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BuildingEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithSiteAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Site!.Id == siteId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateSiteNameAsync(
        string siteId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Site!.Id == siteId,
            Builders<BuildingEntity>.Update.Set(x => x.Site!.Name, siteName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<BuildingEntity>(
                Builders<BuildingEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_building_client_code"
                }),
            new CreateIndexModel<BuildingEntity>(
                Builders<BuildingEntity>.IndexKeys
                    .Ascending(x => x.Site!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_building_site"
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
