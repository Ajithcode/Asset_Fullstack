using System.Text.RegularExpressions;
using BusinessUnitEntity = A360.MasterDataManagement.Domain.Entities.BusinessUnit;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class BusinessUnitRepository : MongoRepository<BusinessUnitEntity>,
    IBusinessUnitRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_business_units";

    public BusinessUnitRepository(IMongoDatabase database)
        : base(database.GetCollection<BusinessUnitEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<BusinessUnitEntity>> SearchAsync(
        BusinessUnitFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BusinessUnitEntity>.Filter;
        var filters = new List<FilterDefinition<BusinessUnitEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.BusinessUnitName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        // Reference filters match either the stored id or the name, since ids are optional for now.
        if (!string.IsNullOrWhiteSpace(filter.ParentOrganization))
        {
            filters.Add(builder.Or(
                builder.Eq(x => x.ParentOrganization!.Id, filter.ParentOrganization),
                builder.Regex(x => x.ParentOrganization!.Name, ExactIgnoreCase(filter.ParentOrganization))));
        }

        if (!string.IsNullOrWhiteSpace(filter.Department))
        {
            filters.Add(builder.ElemMatch(x => x.Departments, ReferenceMatches(filter.Department)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Site))
        {
            filters.Add(builder.ElemMatch(x => x.Sites, ReferenceMatches(filter.Site)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.BusinessUnitName)
            .ToListAsync(cancellationToken);
    }

    public async Task<BusinessUnitEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BusinessUnitEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithParentOrganizationAsync(
        string organizationId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentOrganization!.Id == organizationId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateParentOrganizationNameAsync(
        string organizationId,
        string organizationName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.ParentOrganization!.Id == organizationId,
            Builders<BusinessUnitEntity>.Update.Set(x => x.ParentOrganization!.Name, organizationName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateDepartmentNameAsync(
        string departmentId,
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        // Validation rejects duplicate entries, so the positional "$" (first match) covers each document.
        await Collection.UpdateManyAsync(
            Builders<BusinessUnitEntity>.Filter.ElemMatch(x => x.Departments, x => x.Id == departmentId),
            Builders<BusinessUnitEntity>.Update.Set("departments.$.name", departmentName),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            Builders<BusinessUnitEntity>.Filter.ElemMatch(x => x.Departments, x => x.Id == departmentId),
            Builders<BusinessUnitEntity>.Update.PullFilter(x => x.Departments, x => x.Id == departmentId),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateSiteNameAsync(
        string siteId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        // Validation rejects duplicate entries, so the positional "$" (first match) covers each document.
        await Collection.UpdateManyAsync(
            Builders<BusinessUnitEntity>.Filter.ElemMatch(x => x.Sites, x => x.Id == siteId),
            Builders<BusinessUnitEntity>.Update.Set("sites.$.name", siteName),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveSiteAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            Builders<BusinessUnitEntity>.Filter.ElemMatch(x => x.Sites, x => x.Id == siteId),
            Builders<BusinessUnitEntity>.Update.PullFilter(x => x.Sites, x => x.Id == siteId),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<BusinessUnitEntity>(
                Builders<BusinessUnitEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_business_unit_client_code"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static FilterDefinition<MasterDataReference> ReferenceMatches(string value)
    {
        var builder = Builders<MasterDataReference>.Filter;
        return builder.Or(
            builder.Eq(x => x.Id, value),
            builder.Regex(x => x.Name, ExactIgnoreCase(value)));
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
