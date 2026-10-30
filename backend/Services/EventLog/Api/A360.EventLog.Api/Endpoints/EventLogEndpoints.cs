using A360.EventLog.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.EventLog.Api.Endpoints;

public static class EventLogEndpoints
{
    public static RouteGroupBuilder MapEventLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/eventlogs").WithTags("EventLogs");

        group.MapGet("", GetEventLogsAsync).WithName("GetEventLogs");
        group.MapGet("/{id}", GetEventLogByIdAsync).WithName("GetEventLogById");

        return group;
    }

    private static async Task<IResult> GetEventLogsAsync(
        IEventLogRepository repository,
        CancellationToken cancellationToken,
        string? serviceName = null,
        string? entityType = null,
        string? entityId = null,
        string? action = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50,
        string? performedBy = null)
    {
        var result = await repository.QueryAsync(
            new EventLogQuery(serviceName, entityType, entityId, action, from, to, page, pageSize, performedBy),
            cancellationToken);

        var response = new EventLogListResponse(
            result.Items.Select(EventLogResponse.FromEntity).ToList(),
            result.TotalCount,
            page < 1 ? 1 : page,
            pageSize is < 1 or > 200 ? 50 : pageSize);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetEventLogByIdAsync(
        string id,
        IEventLogRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid event log id." });
        }

        var eventLog = await repository.GetByIdAsync(id, cancellationToken);
        return eventLog is null ? Results.NotFound() : Results.Ok(EventLogResponse.FromEntity(eventLog));
    }
}
