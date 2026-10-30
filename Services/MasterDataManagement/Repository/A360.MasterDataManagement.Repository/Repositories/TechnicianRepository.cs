using System.Text.RegularExpressions;
using TechnicianEntity = A360.MasterDataManagement.Domain.Entities.Technician;
using A360.MasterDataManagement.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class TechnicianRepository : MongoRepository<TechnicianEntity>,
    ITechnicianRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_technicians";

    public TechnicianRepository(IMongoDatabase database)
        : base(database.GetCollection<TechnicianEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<TechnicianEntity>> SearchAsync(
        TechnicianFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;
        var filters = new List<FilterDefinition<TechnicianEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.TechnicianName, pattern),
                builder.Regex(x => x.TechnicianId, pattern),
                builder.Regex(x => x.Email, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.PrimarySkill))
        {
            filters.Add(builder.Regex(x => x.PrimarySkill, ExactIgnoreCase(filter.PrimarySkill)));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssignedTeamId))
        {
            filters.Add(builder.Eq(x => x.AssignedTeam!.Id, filter.AssignedTeamId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.TechnicianName)
            .ToListAsync(cancellationToken);
    }

    public async Task<TechnicianEntity?> GetByTechnicianIdAsync(
        string technicianId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;
        var filter = builder.Regex(x => x.TechnicianId, ExactIgnoreCase(technicianId))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TechnicianEntity>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<TechnicianEntity>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TechnicianEntity>> GetByTeamsAsync(
        IEnumerable<string> teamIds,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<TechnicianEntity>.Filter.In(x => x.AssignedTeam!.Id, teamIds))
            .SortBy(x => x.TechnicianName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TechnicianEntity>> FindByReferenceAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;
        var filters = new List<FilterDefinition<TechnicianEntity>>
        {
            builder.Regex(x => x.TechnicianId, ExactIgnoreCase(value)),
            builder.Regex(x => x.TechnicianName, ExactIgnoreCase(value))
        };

        if (ObjectId.TryParse(value.Trim(), out _))
        {
            filters.Add(builder.Eq(x => x.Id, value.Trim()));
        }

        return await Collection
            .Find(builder.Or(filters))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyWithTeamAsync(
        string teamId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.AssignedTeam!.Id == teamId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateTeamNameAsync(
        string teamId,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.AssignedTeam!.Id == teamId,
            Builders<TechnicianEntity>.Update.Set(x => x.AssignedTeam!.Name, teamName),
            cancellationToken: cancellationToken);
    }

    public async Task SetTeamMembersAsync(
        string teamId,
        string teamName,
        IReadOnlyCollection<string> technicianIds,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.AssignedTeam!.Id, teamId) & builder.Nin(x => x.Id, technicianIds),
            Builders<TechnicianEntity>.Update.Set(x => x.AssignedTeam, null),
            cancellationToken: cancellationToken);

        if (technicianIds.Count > 0)
        {
            await Collection.UpdateManyAsync(
                builder.In(x => x.Id, technicianIds),
                Builders<TechnicianEntity>.Update.Set(
                    x => x.AssignedTeam,
                    new MasterDataReference { Id = teamId, Name = teamName }),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<long> CountWithSkillAsync(
        string skillName,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;
        var filter = (builder.Eq(x => x.PrimarySkill, skillName) | builder.AnyEq(x => x.SecondarySkills, skillName))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task RenameSkillAsync(
        string oldName,
        string newName,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TechnicianEntity>.Filter;
        await Collection.UpdateManyAsync(
            builder.Eq(x => x.PrimarySkill, oldName) & builder.Eq(x => x.ClientId, clientId),
            Builders<TechnicianEntity>.Update.Set(x => x.PrimarySkill, newName),
            cancellationToken: cancellationToken);

        await Collection.UpdateManyAsync(
            builder.AnyEq(x => x.SecondarySkills, oldName) & builder.Eq(x => x.ClientId, clientId),
            Builders<TechnicianEntity>.Update.Set("secondary_skills.$[skill]", newName),
            new UpdateOptions
            {
                ArrayFilters =
                [
                    new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("skill", oldName))
                ]
            },
            cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TechnicianEntity>(
                Builders<TechnicianEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.TechnicianId),
                new CreateIndexOptions
                {
                    Name = "ix_master_technician_client_technician_id"
                }),
            new CreateIndexModel<TechnicianEntity>(
                Builders<TechnicianEntity>.IndexKeys
                    .Ascending(x => x.AssignedTeam!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_technician_team"
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
