using System.Text.RegularExpressions;
using ShiftEntity = A360.MasterDataManagement.Domain.Entities.Shift;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ShiftRepository : MongoRepository<ShiftEntity>,
    IShiftRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_shifts";

    public ShiftRepository(IMongoDatabase database)
        : base(database.GetCollection<ShiftEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ShiftEntity>> SearchAsync(
        ShiftFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ShiftEntity>.Filter;
        var filters = new List<FilterDefinition<ShiftEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ShiftName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ShiftTime))
        {
            filters.Add(builder.Regex(x => x.ShiftTime, ExactIgnoreCase(filter.ShiftTime)));
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkingDay))
        {
            filters.Add(builder.AnyEq(x => x.WorkingDays, filter.WorkingDay));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        // "HH:mm" strings sort in time order.
        return await Collection
            .Find(combined)
            .SortBy(x => x.StartTime)
            .ThenBy(x => x.ShiftName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ShiftEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ShiftEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ShiftEntity>(
                Builders<ShiftEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_shift_client_code"
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
