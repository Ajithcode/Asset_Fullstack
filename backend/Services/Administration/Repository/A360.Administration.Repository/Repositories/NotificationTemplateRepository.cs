using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class NotificationTemplateRepository
    : MongoRepository<NotificationTemplate>, INotificationTemplateRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_notification_templates";

    public NotificationTemplateRepository(IMongoDatabase database)
        : base(database.GetCollection<NotificationTemplate>(CollectionName))
    {
    }

    public async Task<bool> TemplateCodeExistsAsync(
        string templateCode,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NotificationTemplate>.Filter;
        var filter = builder.Eq(template => template.TemplateCode, templateCode)
            & builder.Eq(template => template.IsDeleted, false)
            & builder.Eq(template => template.TenantId, tenantId);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys.Ascending(template => template.TemplateId),
                new CreateIndexOptions { Name = "ux_administration_notification_templates_template_id", Unique = true }),
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys
                    .Ascending(template => template.TenantId)
                    .Ascending(template => template.TemplateCode),
                new CreateIndexOptions { Name = "ix_administration_notification_templates_tenant_id_template_code" }),
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys.Ascending(template => template.TriggerEvent),
                new CreateIndexOptions { Name = "ix_administration_notification_templates_trigger_event" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
