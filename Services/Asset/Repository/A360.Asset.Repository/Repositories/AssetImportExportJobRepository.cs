using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetImportExportJobRepository
    : MongoRepository<AssetImportExportJob>, IAssetImportExportJobRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetImportExportJob";

    public const string ImportType = "Import";
    public const string ExportType = "Export";

    // Import: Uploaded → Mapped → Validated → Completed / Failed. Export: Completed.
    public const string UploadedStatus = "Uploaded";
    public const string MappedStatus = "Mapped";
    public const string ValidatedStatus = "Validated";
    public const string CompletedStatus = "Completed";
    public const string FailedStatus = "Failed";

    public AssetImportExportJobRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetImportExportJob>(CollectionName))
    {
    }

    public async Task<AssetImportExportHistoryResult> QueryHistoryAsync(AssetImportExportHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetImportExportJob>>
        {
            Builders<AssetImportExportJob>.Filter.Eq(job => job.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            filters.Add(Builders<AssetImportExportJob>.Filter.Eq(job => job.Type, query.Type.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(Builders<AssetImportExportJob>.Filter.Eq(job => job.Status, query.Status.Trim()));
        }
        else if (!query.IncludeInProgress)
        {
            filters.Add(Builders<AssetImportExportJob>.Filter.In(job => job.Status, new[] { CompletedStatus, FailedStatus }));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetImportExportJob>.Filter.Or(
                Builders<AssetImportExportJob>.Filter.Regex(job => job.FileName, pattern),
                Builders<AssetImportExportJob>.Filter.Regex(job => job.JobNumber, pattern),
                Builders<AssetImportExportJob>.Filter.Regex(job => job.CreatedBy, pattern)));
        }

        var filter = Builders<AssetImportExportJob>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        // The history only needs the counts, so the stored file is left behind.
        var items = await Collection
            .Find(filter)
            .Project<AssetImportExportJob>(Builders<AssetImportExportJob>.Projection
                .Exclude(job => job.Import!.Rows)
                .Exclude(job => job.Import!.Columns))
            .SortByDescending(job => job.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return new AssetImportExportHistoryResult(items, totalCount);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetImportExportJob>(
                Builders<AssetImportExportJob>.IndexKeys.Ascending(job => job.JobNumber),
                new CreateIndexOptions { Name = "ix_asset_import_export_job_number" }),
            new CreateIndexModel<AssetImportExportJob>(
                Builders<AssetImportExportJob>.IndexKeys
                    .Ascending(job => job.Type)
                    .Descending(job => job.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_import_export_type_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
