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
}
