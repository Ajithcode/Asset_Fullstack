namespace A360.Repository.Activity;

public interface IEventLogger
{
    /// <summary>Logs an event performed by the user signed in on the current request (if any).</summary>
    Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>Logs an event performed by an explicitly named user, e.g. during login before a token exists.</summary>
    Task LogAsync(
        string entityType,
        string entityId,
        string entityName,
        string action,
        EventActor performedBy,
        CancellationToken cancellationToken = default);
}

public sealed record EventActor(string? RefId, string UserName, string? ClientId = null, string? TenantId = null);
