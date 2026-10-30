using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface INotificationTemplateRepository : IMongoRepository<NotificationTemplate>
{
    Task<NotificationTemplate?> GetByTemplateIdAsync(string templateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationTemplate>> GetAsync(
        string? tenantId,
        string? clientId,
        string? category,
        string? status,
        CancellationToken cancellationToken = default);

    // excludeTemplateId is the template being updated, so it does not clash with itself.
    Task<bool> TemplateCodeExistsAsync(
        string templateCode,
        string? tenantId,
        string? excludeTemplateId = null,
        CancellationToken cancellationToken = default);
}
