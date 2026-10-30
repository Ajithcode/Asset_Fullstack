using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface INotificationTemplateRepository : IMongoRepository<NotificationTemplate>
{
    Task<bool> TemplateCodeExistsAsync(string templateCode, string? tenantId, CancellationToken cancellationToken = default);
}
