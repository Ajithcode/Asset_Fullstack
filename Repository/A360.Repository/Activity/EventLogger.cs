using A360.Domain.Entities;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Repository.Activity;

public sealed class EventLogger : IEventLogger
{
    private readonly IEventLogRepository _repository;
    private readonly string _serviceName;
    private readonly ICurrentUserAccessor? _currentUserAccessor;

    public EventLogger(IEventLogRepository repository, string serviceName, ICurrentUserAccessor? currentUserAccessor = null)
    {
        _repository = repository;
        _serviceName = serviceName;
        _currentUserAccessor = currentUserAccessor;
    }

    public Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor?.GetCurrentUser();
        var performedBy = currentUser is null
            ? null
            : new EventActor(currentUser.Id, currentUser.UserName, currentUser.ClientId, currentUser.TenantId);

        return WriteAsync(entityType, entityId, entityName, action, performedBy, cancellationToken);
    }

    public Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        EventActor performedBy,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(entityType, entityId, entityName, action, performedBy, cancellationToken);
    }

    private async Task WriteAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        EventActor? performedBy,
        CancellationToken cancellationToken)
    {
        await _repository.CreateAsync(new EventLog
        {
            ServiceName = _serviceName,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Action = action,
            PerformedBy = performedBy?.UserName,
            PerformedByRefId = performedBy?.RefId,
            CreatedBy = performedBy?.UserName,
            ClientId = performedBy?.ClientId,
            TenantId = performedBy?.TenantId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}
