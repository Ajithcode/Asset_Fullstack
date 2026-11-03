using System.Text.RegularExpressions;
using FloorEntity = A360.MasterDataManagement.Domain.Entities.Floor;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class FloorRepository : MongoRepository<FloorEntity>,
    IFloorRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_floors";

    public FloorRepository(IMongoDatabase database)
        : base(database.GetCollection<FloorEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<FloorEntity>> SearchAsync(
        FloorFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<FloorEntity>.Filter;
        var filters = new List<FilterDefinition<FloorEntity>>();

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

        if (!string.IsNullOrWhiteSpace(filter.BuildingId))
        {
            filters.Add(builder.Eq(x => x.BuildingId, filter.BuildingId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.BuildingName)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<FloorEntity?> GetByCodeAsync(
        string code,
        string buildingId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<FloorEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.BuildingId, buildingId)
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

    public async Task<bool> AnyWithBuildingAsync(
        string buildingId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.BuildingId == buildingId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateSiteNameAsync(
        string siteId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Site!.Id == siteId,
            Builders<FloorEntity>.Update.Set(x => x.Site!.Name, siteName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateBuildingNameAsync(
        string buildingId,
        string buildingName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.BuildingId == buildingId,
            Builders<FloorEntity>.Update.Set(x => x.BuildingName, buildingName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<FloorEntity>(
                Builders<FloorEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.BuildingId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_floor_client_building_code"
                }),
            new CreateIndexModel<FloorEntity>(
                Builders<FloorEntity>.IndexKeys
                    .Ascending(x => x.BuildingId),
                new CreateIndexOptions
                {
                    Name = "ix_master_floor_building"
                }),
            new CreateIndexModel<FloorEntity>(
                Builders<FloorEntity>.IndexKeys
                    .Ascending(x => x.Site!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_floor_site"
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
