using System.Text.RegularExpressions;
using EmployeeEntity = A360.MasterDataManagement.Domain.Entities.Employee;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class EmployeeRepository : MongoRepository<EmployeeEntity>,
    IEmployeeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_employees";

    public EmployeeRepository(IMongoDatabase database)
        : base(database.GetCollection<EmployeeEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<EmployeeEntity>> SearchAsync(
        EmployeeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<EmployeeEntity>.Filter;
        var filters = new List<FilterDefinition<EmployeeEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.EmployeeName, pattern),
                builder.Regex(x => x.EmployeeId, pattern),
                builder.Regex(x => x.Email, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Designation))
        {
            filters.Add(builder.Regex(x => x.Designation, ExactIgnoreCase(filter.Designation)));
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
            .SortBy(x => x.EmployeeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeEntity?> GetByEmployeeIdAsync(
        string employeeId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<EmployeeEntity>.Filter;
        var filter = builder.Regex(x => x.EmployeeId, ExactIgnoreCase(employeeId))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeEntity>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<EmployeeEntity>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeEntity>> GetByDepartmentsAsync(
        IEnumerable<string> departmentIds,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<EmployeeEntity>.Filter.In(x => x.Department!.Id, departmentIds))
            .SortBy(x => x.EmployeeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeEntity>> FindByReferenceAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<EmployeeEntity>.Filter;
        var filters = new List<FilterDefinition<EmployeeEntity>>
        {
            builder.Regex(x => x.EmployeeId, ExactIgnoreCase(value)),
            builder.Regex(x => x.EmployeeName, ExactIgnoreCase(value))
        };

        if (ObjectId.TryParse(value.Trim(), out _))
        {
            filters.Add(builder.Eq(x => x.Id, value.Trim()));
        }

        return await Collection
            .Find(builder.Or(filters))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyWithDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Department!.Id == departmentId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateDepartmentNameAsync(
        string departmentId,
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Department!.Id == departmentId,
            Builders<EmployeeEntity>.Update.Set(x => x.Department!.Name, departmentName),
            cancellationToken: cancellationToken);
    }

    public async Task SetDepartmentEmployeesAsync(
        string departmentId,
        string departmentName,
        IReadOnlyCollection<string> employeeIds,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<EmployeeEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.Department!.Id, departmentId) & builder.Nin(x => x.Id, employeeIds),
            Builders<EmployeeEntity>.Update.Set(x => x.Department, null),
            cancellationToken: cancellationToken);

        if (employeeIds.Count > 0)
        {
            await Collection.UpdateManyAsync(
                builder.In(x => x.Id, employeeIds),
                Builders<EmployeeEntity>.Update.Set(
                    x => x.Department,
                    new MasterDataReference { Id = departmentId, Name = departmentName }),
                cancellationToken: cancellationToken);
        }
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<EmployeeEntity>(
                Builders<EmployeeEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.EmployeeId),
                new CreateIndexOptions
                {
                    Name = "ix_master_employee_client_employee_id"
                }),
            new CreateIndexModel<EmployeeEntity>(
                Builders<EmployeeEntity>.IndexKeys
                    .Ascending(x => x.Department!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_employee_department"
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
