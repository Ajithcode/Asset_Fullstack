using System.Text.RegularExpressions;
using TeamEntity = A360.MasterDataManagement.Domain.Entities.Team;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class TeamRepository : MongoRepository<TeamEntity>,
    ITeamRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_teams";

    public TeamRepository(IMongoDatabase database)
        : base(database.GetCollection<TeamEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<TeamEntity>> SearchAsync(
        TeamFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TeamEntity>.Filter;
        var filters = new List<FilterDefinition<TeamEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.TeamName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentId))
        {
            filters.Add(builder.Eq(x => x.Department!.Id, filter.DepartmentId));
        }

        if (!string.IsNullOrWhiteSpace(filter.TeamLeaderId))
        {
            filters.Add(builder.Eq(x => x.TeamLeader!.Id, filter.TeamLeaderId));
        }

        if (filter.TeamIds is not null)
        {
            filters.Add(builder.In(x => x.Id, filter.TeamIds));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.TeamName)
            .ToListAsync(cancellationToken);
    }

    public async Task<TeamEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TeamEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Department!.Id == departmentId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithTeamLeaderAsync(
        string technicianId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.TeamLeader!.Id == technicianId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateDepartmentNameAsync(
        string departmentId,
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Department!.Id == departmentId,
            Builders<TeamEntity>.Update.Set(x => x.Department!.Name, departmentName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateTeamLeaderNameAsync(
        string technicianId,
        string technicianName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.TeamLeader!.Id == technicianId,
            Builders<TeamEntity>.Update.Set(x => x.TeamLeader!.Name, technicianName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TeamEntity>(
                Builders<TeamEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_team_client_code"
                }),
            new CreateIndexModel<TeamEntity>(
                Builders<TeamEntity>.IndexKeys
                    .Ascending(x => x.Department!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_team_department"
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
