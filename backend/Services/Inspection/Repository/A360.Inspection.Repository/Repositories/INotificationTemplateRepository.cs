using A360.Repository.Repositories;
using NotificationTemplateEntity = A360.Inspection.Domain.Entities.NotificationTemplate;

namespace A360.Inspection.Repository.Repositories;

public interface INotificationTemplateRepository : IMongoRepository<NotificationTemplateEntity>
{
    Task<NotificationTemplateEntity?> GetByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default);
}
