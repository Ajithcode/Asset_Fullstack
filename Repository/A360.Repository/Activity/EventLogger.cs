using System.Net;
using A360.Domain.Entities;
using A360.Repository.Repositories;
using A360.Security;
using Microsoft.AspNetCore.Http;

namespace A360.Repository.Activity;

public sealed class EventLogger : IEventLogger
{
    private readonly IEventLogRepository _repository;
    private readonly string _serviceName;
    private readonly ICurrentUserAccessor? _currentUserAccessor;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public EventLogger(
        IEventLogRepository repository,
        string serviceName,
        ICurrentUserAccessor? currentUserAccessor = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _repository = repository;
        _serviceName = serviceName;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(entityType, entityId, entityName, action, CurrentActor(), null, cancellationToken);
    }

    public Task LogDetailedAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        string? details,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(entityType, entityId, entityName, action, CurrentActor(), details, cancellationToken);
    }

    public Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        EventActor performedBy,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(entityType, entityId, entityName, action, performedBy, null, cancellationToken);
    }

    private EventActor? CurrentActor()
    {
        var currentUser = _currentUserAccessor?.GetCurrentUser();
        return currentUser is null
            ? null
            : new EventActor(currentUser.Id, currentUser.UserName, currentUser.ClientId, currentUser.TenantId);
    }

    private async Task WriteAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        EventActor? performedBy,
        string? details,
        CancellationToken cancellationToken)
    {
        var eventLog = new EventLog
        {
            ServiceName = _serviceName,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Action = action,
            Details = string.IsNullOrWhiteSpace(details) ? null : details,
            PerformedBy = performedBy?.UserName,
            PerformedByRefId = performedBy?.RefId,
            CreatedBy = performedBy?.UserName,
            ClientId = performedBy?.ClientId,
            TenantId = performedBy?.TenantId,
            CreatedAt = DateTime.UtcNow
        };

        var httpContext = _httpContextAccessor?.HttpContext;
        AddRequestContext(eventLog, httpContext);
        AuditClassifier.Apply(eventLog);

        await _repository.CreateAsync(eventLog, cancellationToken);

        // The endpoint logged its own event: WriteAuditMiddleware adds none for this request
        if (httpContext is not null)
        {
            httpContext.Items[WriteAuditMiddleware.EventLoggedKey] = true;
        }
    }

    /// <summary>For the System Audit Log: caller's IP, browser, request id, and the signed-in user's email and role.</summary>
    private static void AddRequestContext(EventLog eventLog, HttpContext? context)
    {
        if (context is null)
        {
            return;
        }

        var headers = context.Request.Headers;
        eventLog.IpAddress = ClientIpOf(context);
        eventLog.UserAgent = Limit(headers.UserAgent.ToString(), 512);
        eventLog.RequestId = Limit(FirstNonEmpty(headers["X-Request-Id"].ToString(), headers["X-Correlation-Id"].ToString())
            ?? context.TraceIdentifier, 128);

        // Only when the signed-in user is the one acting (not an explicitly named user during sign-in)
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true
            && string.Equals(principal.FindFirst(A360ClaimTypes.UserName)?.Value, eventLog.PerformedBy, StringComparison.Ordinal))
        {
            eventLog.PerformedByEmail = principal.FindFirst(A360ClaimTypes.Email)?.Value;
            var roles = principal.FindAll(A360ClaimTypes.Role).Select(claim => claim.Value).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct().ToList();
            eventLog.PerformedByRole = roles.Count == 0 ? null : string.Join(", ", roles);
        }
    }

    /// <summary>The browser's address: first X-Forwarded-For entry (set by the gateway / proxy), else the connection.</summary>
    private static string? ClientIpOf(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        var candidate = FirstNonEmpty(forwarded.Split(',').FirstOrDefault()?.Trim(), context.Request.Headers["X-Real-IP"].ToString());
        if (candidate is not null && IPAddress.TryParse(candidate, out var parsed))
        {
            return (parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed).ToString();
        }

        var remote = context.Connection.RemoteIpAddress;
        return remote is null ? null : (remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote).ToString();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? Limit(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= length ? value : value[..length];
}
