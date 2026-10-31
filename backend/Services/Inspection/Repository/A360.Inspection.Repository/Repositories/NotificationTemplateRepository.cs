using MongoDB.Driver;
using A360.Repository.Repositories;
using NotificationTemplateEntity = A360.Inspection.Domain.Entities.NotificationTemplate;

namespace A360.Inspection.Repository.Repositories;

public sealed class NotificationTemplateRepository : MongoRepository<NotificationTemplateEntity>, INotificationTemplateRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "notification_templates";

    public NotificationTemplateRepository(IMongoDatabase database)
        : base(database.GetCollection<NotificationTemplateEntity>(CollectionName))
    {
    }

    public async Task<NotificationTemplateEntity?> GetByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(template => template.TemplateCode == templateCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<NotificationTemplateEntity>(
                Builders<NotificationTemplateEntity>.IndexKeys.Ascending(template => template.TemplateCode),
                new CreateIndexOptions { Name = "ix_notification_templates_template_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
