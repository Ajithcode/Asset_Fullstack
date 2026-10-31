using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class DataImportJobRepository
    : MongoRepository<DataImportJob>, IDataImportJobRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_data_import_jobs";

    public DataImportJobRepository(IMongoDatabase database)
        : base(database.GetCollection<DataImportJob>(CollectionName))
    {
    }

    public async Task<DataImportJob?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        return await Collection
            .Find(job => job.JobId == jobId && !job.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DataImportJob>> GetAsync(
        string? tenantId,
        string? clientId,
        string? dataObject,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<DataImportJob>.Filter;
        var filter = builder.Eq(job => job.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(job => job.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(job => job.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(dataObject))
        {
            filter &= builder.Regex(job => job.DataObject, ExactIgnoreCase(dataObject));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(job => job.Status, ExactIgnoreCase(status));
        }

        // Newest first, like the import job list.
        return await Collection
            .Find(filter)
            .SortByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DataImportJob>(
                Builders<DataImportJob>.IndexKeys.Ascending(job => job.JobId),
                new CreateIndexOptions { Name = "ux_administration_data_import_jobs_job_id", Unique = true }),
            new CreateIndexModel<DataImportJob>(
                Builders<DataImportJob>.IndexKeys
                    .Ascending(job => job.TenantId)
                    .Ascending(job => job.DataObject),
                new CreateIndexOptions { Name = "ix_administration_data_import_jobs_tenant_id_data_object" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
