using MongoDB.Bson;
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

    public async Task<NotificationTemplate?> GetByTemplateIdAsync(string templateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        return await Collection
            .Find(template => template.TemplateId == templateId && !template.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<NotificationTemplate>> GetAsync(
        string? tenantId,
        string? clientId,
        string? category,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NotificationTemplate>.Filter;
        var filter = builder.Eq(template => template.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(template => template.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(template => template.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filter &= builder.Regex(template => template.Category, ExactIgnoreCase(category));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(template => template.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(template => template.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TemplateCodeExistsAsync(
        string templateCode,
        string? tenantId,
        string? excludeTemplateId = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<NotificationTemplate>.Filter;
        var filter = builder.Eq(template => template.TemplateCode, templateCode)
            & builder.Eq(template => template.IsDeleted, false)
            & builder.Eq(template => template.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeTemplateId))
        {
            filter &= builder.Ne(template => template.TemplateId, excludeTemplateId);
        }

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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
