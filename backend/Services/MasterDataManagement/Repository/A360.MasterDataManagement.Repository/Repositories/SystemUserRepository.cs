using System.Text.RegularExpressions;
using SystemUserEntity = A360.MasterDataManagement.Domain.Entities.SystemUser;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class SystemUserRepository : MongoRepository<SystemUserEntity>,
    ISystemUserRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_users";

    public SystemUserRepository(IMongoDatabase database)
        : base(database.GetCollection<SystemUserEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SystemUserEntity>> SearchAsync(
        SystemUserFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SystemUserEntity>.Filter;
        var filters = new List<FilterDefinition<SystemUserEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.Code, pattern),
                builder.Regex(x => x.FullName, pattern),
                builder.Regex(x => x.Username, pattern),
                builder.Regex(x => x.Email, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.PrimaryRoleId))
        {
            filters.Add(builder.Eq(x => x.PrimaryRole!.Id, filter.PrimaryRoleId));
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
            .SortBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SystemUserEntity?> GetByEmailAsync(
        string email,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SystemUserEntity>.Filter;
        var filter = builder.Regex(x => x.Email, ExactIgnoreCase(email))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SystemUserEntity?> GetByUsernameAsync(
        string username,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SystemUserEntity>.Filter;
        var filter = builder.Regex(x => x.Username, ExactIgnoreCase(username))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SystemUserEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SystemUserEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithAdditionalRoleAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<SystemUserEntity>.Filter.ElemMatch(x => x.AdditionalRoles, r => r.Id == roleId))
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateAdditionalRoleNameAsync(
        string roleId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        // Each user lists a role at most once, so the positional "$" update reaches it.
        await Collection.UpdateManyAsync(
            Builders<SystemUserEntity>.Filter.ElemMatch(x => x.AdditionalRoles, r => r.Id == roleId),
            Builders<SystemUserEntity>.Update.Set("additional_roles.$.name", roleName),
            cancellationToken: cancellationToken);
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
            Builders<SystemUserEntity>.Update.Set(x => x.Department!.Name, departmentName),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> AnyWithPrimaryRoleAsync(
        string primaryRoleId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.PrimaryRole!.Id == primaryRoleId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdatePrimaryRoleNameAsync(
        string primaryRoleId,
        string primaryRoleName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.PrimaryRole!.Id == primaryRoleId,
            Builders<SystemUserEntity>.Update.Set(x => x.PrimaryRole!.Name, primaryRoleName),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByPrimaryRoleAsync(
        IEnumerable<string> primaryRoleIds,
        CancellationToken cancellationToken = default)
    {
        var counts = await Collection
            .Aggregate()
            .Match(Builders<SystemUserEntity>.Filter.In(x => x.PrimaryRole!.Id, primaryRoleIds))
            .Group(x => x.PrimaryRole!.Id, g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.Id!, x => x.Count);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SystemUserEntity>(
                Builders<SystemUserEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Username),
                new CreateIndexOptions
                {
                    Name = "ix_master_user_client_username"
                }),
            new CreateIndexModel<SystemUserEntity>(
                Builders<SystemUserEntity>.IndexKeys
                    .Ascending(x => x.Department!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_user_department"
                }),
            new CreateIndexModel<SystemUserEntity>(
                Builders<SystemUserEntity>.IndexKeys
                    .Ascending(x => x.PrimaryRole!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_user_primary_role"
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
