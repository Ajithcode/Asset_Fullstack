using System.Text.RegularExpressions;
using DepartmentEntity = A360.MasterDataManagement.Domain.Entities.Department;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class DepartmentRepository : MongoRepository<DepartmentEntity>,
    IDepartmentRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_departments";

    public DepartmentRepository(IMongoDatabase database)
        : base(database.GetCollection<DepartmentEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<DepartmentEntity>> SearchAsync(
        DepartmentFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<DepartmentEntity>.Filter;
        var filters = new List<FilterDefinition<DepartmentEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.DepartmentName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.BusinessUnitId))
        {
            filters.Add(builder.Eq(x => x.BusinessUnit!.Id, filter.BusinessUnitId));
        }

        // Reference filters match either the stored id or the name, since ids are optional.
        if (!string.IsNullOrWhiteSpace(filter.DepartmentHead))
        {
            filters.Add(builder.Or(
                builder.Eq(x => x.DepartmentHead!.Id, filter.DepartmentHead),
                builder.Regex(x => x.DepartmentHead!.Name, ExactIgnoreCase(filter.DepartmentHead))));
        }

        if (filter.DepartmentIds is not null)
        {
            filters.Add(builder.In(x => x.Id, filter.DepartmentIds));
        }

        if (!string.IsNullOrWhiteSpace(filter.Asset))
        {
            filters.Add(builder.ElemMatch(x => x.Assets, ReferenceMatches(filter.Asset)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.DepartmentName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyWithDepartmentHeadAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.DepartmentHead!.Id == employeeId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateDepartmentHeadNameAsync(
        string employeeId,
        string employeeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.DepartmentHead!.Id == employeeId,
            Builders<DepartmentEntity>.Update.Set(x => x.DepartmentHead!.Name, employeeName),
            cancellationToken: cancellationToken);
    }

    public async Task<DepartmentEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<DepartmentEntity>.Filter;
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

    public async Task UpdateBusinessUnitNameAsync(
        string businessUnitId,
        string businessUnitName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.BusinessUnit!.Id == businessUnitId,
            Builders<DepartmentEntity>.Update.Set(x => x.BusinessUnit!.Name, businessUnitName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DepartmentEntity>(
                Builders<DepartmentEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_department_client_code"
                }),
            new CreateIndexModel<DepartmentEntity>(
                Builders<DepartmentEntity>.IndexKeys
                    .Ascending(x => x.BusinessUnit!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_department_business_unit"
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
