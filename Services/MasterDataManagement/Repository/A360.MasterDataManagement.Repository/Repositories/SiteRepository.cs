using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SiteEntity = A360.MasterDataManagement.Domain.Entities.Site;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class SiteRepository : MongoRepository<SiteEntity>,
    ISiteRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_sites";

    public SiteRepository(IMongoDatabase database)
        : base(database.GetCollection<SiteEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SiteEntity>> SearchAsync(
        SiteFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SiteEntity>.Filter;
        var filters = new List<FilterDefinition<SiteEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.SiteName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        AddEqualsIgnoreCase(filters, x => x.Status, filter.Status);
        AddEqualsIgnoreCase(filters, x => x.SiteType, filter.SiteType);
        AddEqualsIgnoreCase(filters, x => x.City, filter.City);
        AddEqualsIgnoreCase(filters, x => x.Country, filter.Country);

        if (!string.IsNullOrWhiteSpace(filter.BusinessUnitId))
        {
            filters.Add(builder.Eq(x => x.BusinessUnit!.Id, filter.BusinessUnitId));
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentId))
        {
            filters.Add(builder.Eq(x => x.Department!.Id, filter.DepartmentId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.SiteName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SiteEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SiteEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithBusinessUnitAsync(
        string businessUnitId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.BusinessUnit!.Id == businessUnitId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Department!.Id == departmentId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateBusinessUnitNameAsync(
        string businessUnitId,
        string businessUnitName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.BusinessUnit!.Id == businessUnitId,
            Builders<SiteEntity>.Update.Set(x => x.BusinessUnit!.Name, businessUnitName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateDepartmentNameAsync(
        string departmentId,
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Department!.Id == departmentId,
            Builders<SiteEntity>.Update.Set(x => x.Department!.Name, departmentName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SiteEntity>(
                Builders<SiteEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_site_client_code"
                }),
            new CreateIndexModel<SiteEntity>(
                Builders<SiteEntity>.IndexKeys
                    .Ascending(x => x.BusinessUnit!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_site_business_unit"
                }),
            new CreateIndexModel<SiteEntity>(
                Builders<SiteEntity>.IndexKeys
                    .Ascending(x => x.Department!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_site_department"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static void AddEqualsIgnoreCase(
        List<FilterDefinition<SiteEntity>> filters,
        Expression<Func<SiteEntity, object?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<SiteEntity>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
