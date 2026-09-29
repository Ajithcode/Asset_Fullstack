using System.Text.RegularExpressions;
using EventLogEntity = A360.Domain.Entities.EventLog;

namespace A360.EventLog.Api.Contracts;

public sealed record EventLogResponse(
    string Id,
    string ServiceName,
    string EntityType,
    string EntityId,
    string EntityName,
    string Action,
    string? PerformedBy,
    DateTime? CreatedAt,
    string Message)
{
    public static EventLogResponse FromEntity(EventLogEntity eventLog)
    {
        return new EventLogResponse(
            eventLog.Id,
            eventLog.ServiceName,
            eventLog.EntityType,
            eventLog.EntityId,
            eventLog.EntityName,
            eventLog.Action,
            eventLog.PerformedBy,
            eventLog.CreatedAt,
            BuildMessage(eventLog));
    }

    private static string BuildMessage(EventLogEntity eventLog)
    {
        var action = string.IsNullOrWhiteSpace(eventLog.Action)
            ? eventLog.Action
            : Regex.Replace(eventLog.Action, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();

        var message = $"{eventLog.EntityName} is {action} with id {eventLog.EntityId}";
        return string.IsNullOrWhiteSpace(eventLog.PerformedBy) ? message : $"{message} by {eventLog.PerformedBy}";
    }
}

public sealed record EventLogListResponse(
    IReadOnlyCollection<EventLogResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);
