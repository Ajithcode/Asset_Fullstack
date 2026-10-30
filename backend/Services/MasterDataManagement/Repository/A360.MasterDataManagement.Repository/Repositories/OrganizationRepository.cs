using System.Text.RegularExpressions;
using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class OrganizationRepository : MongoRepository<OrganizationEntity>,
    IOrganizationRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_organizations";

    public OrganizationRepository(IMongoDatabase database)
        : base(database.GetCollection<OrganizationEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<OrganizationEntity>> SearchAsync(
        OrganizationFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<OrganizationEntity>.Filter;
        var filters = new List<FilterDefinition<OrganizationEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.OrganizationName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filters.Add(builder.Regex(x => x.Type, ExactIgnoreCase(filter.Type)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentOrganizationId))
        {
            filters.Add(builder.Eq(x => x.ParentOrganization!.Id, filter.ParentOrganizationId));
        }

        if (!string.IsNullOrWhiteSpace(filter.BusinessUnit))
        {
            filters.Add(builder.ElemMatch(x => x.BusinessUnits, ReferenceMatches(filter.BusinessUnit)));
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
            .SortBy(x => x.OrganizationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationEntity?> GetByNameAsync(
        string organizationName,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<OrganizationEntity>.Filter;
        var filter = builder.Regex(x => x.OrganizationName, ExactIgnoreCase(organizationName))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<OrganizationEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrganizationEntity>> GetChildrenAsync(
        string parentOrganizationId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentOrganization!.Id == parentOrganizationId)
            .SortBy(x => x.OrganizationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        string parentOrganizationId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentOrganization!.Id == parentOrganizationId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateParentOrganizationNameAsync(
        string parentOrganizationId,
        string parentOrganizationName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.ParentOrganization!.Id == parentOrganizationId,
            Builders<OrganizationEntity>.Update.Set(x => x.ParentOrganization!.Name, parentOrganizationName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateBusinessUnitNameAsync(
        string businessUnitId,
        string businessUnitName,
        CancellationToken cancellationToken = default)
    {
        // Validation rejects duplicate entries, so the positional "$" (first match) covers each document.
        await Collection.UpdateManyAsync(
            Builders<OrganizationEntity>.Filter.ElemMatch(x => x.BusinessUnits, x => x.Id == businessUnitId),
            Builders<OrganizationEntity>.Update.Set("business_units.$.name", businessUnitName),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveBusinessUnitAsync(
        string businessUnitId,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            Builders<OrganizationEntity>.Filter.ElemMatch(x => x.BusinessUnits, x => x.Id == businessUnitId),
            Builders<OrganizationEntity>.Update.PullFilter(x => x.BusinessUnits, x => x.Id == businessUnitId),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateSiteNameAsync(
        string siteId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        // Validation rejects duplicate entries, so the positional "$" (first match) covers each document.
        await Collection.UpdateManyAsync(
            Builders<OrganizationEntity>.Filter.ElemMatch(x => x.Sites, x => x.Id == siteId),
            Builders<OrganizationEntity>.Update.Set("sites.$.name", siteName),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveSiteAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            Builders<OrganizationEntity>.Filter.ElemMatch(x => x.Sites, x => x.Id == siteId),
            Builders<OrganizationEntity>.Update.PullFilter(x => x.Sites, x => x.Id == siteId),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<OrganizationEntity>(
                Builders<OrganizationEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.OrganizationName),
                new CreateIndexOptions
                {
                    Name = "ix_master_organization_client_name"
                }),
            new CreateIndexModel<OrganizationEntity>(
                Builders<OrganizationEntity>.IndexKeys
                    .Ascending(x => x.ParentOrganization!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_organization_parent"
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
