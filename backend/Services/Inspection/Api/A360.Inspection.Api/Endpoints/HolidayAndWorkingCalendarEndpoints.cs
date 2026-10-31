using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class HolidayAndWorkingCalendarEndpoints
{
    private const string SequenceName = "holiday_and_working_calendar";
    private const string CalendarCodePrefix = "CAL";

    public static RouteGroupBuilder MapHolidayAndWorkingCalendarEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/holiday-and-working-calendars").WithTags("HolidayAndWorkingCalendars");

        group.MapGet("", GetHolidayAndWorkingCalendarsAsync).WithName("GetHolidayAndWorkingCalendars");
        group.MapGet("/{id}", GetHolidayAndWorkingCalendarByIdAsync).WithName("GetHolidayAndWorkingCalendarById");
        group.MapPost("", CreateHolidayAndWorkingCalendarAsync).WithName("CreateHolidayAndWorkingCalendar");
        group.MapPut("/{id}", UpdateHolidayAndWorkingCalendarAsync).WithName("UpdateHolidayAndWorkingCalendar");
        group.MapDelete("/{id}", DeleteHolidayAndWorkingCalendarAsync).WithName("DeleteHolidayAndWorkingCalendar");

        return group;
    }

    private static async Task<IResult> GetHolidayAndWorkingCalendarsAsync(
        IHolidayAndWorkingCalendarRepository repository,
        CancellationToken cancellationToken)
    {
        var calendars = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(calendars.Select(HolidayAndWorkingCalendarResponse.FromEntity));
    }

    private static async Task<IResult> GetHolidayAndWorkingCalendarByIdAsync(
        string id,
        IHolidayAndWorkingCalendarRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calendar id." });
        }

        var calendar = await repository.GetByIdAsync(id, cancellationToken);
        if (calendar is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("HolidayAndWorkingCalendar", calendar.CalendarCode, calendar.CalendarName, EventAction.Viewed, cancellationToken);
        return Results.Ok(HolidayAndWorkingCalendarResponse.FromEntity(calendar));
    }

    private static async Task<IResult> CreateHolidayAndWorkingCalendarAsync(
        CreateHolidayAndWorkingCalendarRequest request,
        IHolidayAndWorkingCalendarRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var calendarCode = $"{CalendarCodePrefix}{nextSequence:D6}";

        var calendar = await repository.CreateAsync(request.ToEntity(calendarCode), cancellationToken);

        await eventLogger.LogAsync("HolidayAndWorkingCalendar", calendar.CalendarCode, calendar.CalendarName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/holiday-and-working-calendars/{calendar.Id}", HolidayAndWorkingCalendarResponse.FromEntity(calendar));
    }

    private static async Task<IResult> UpdateHolidayAndWorkingCalendarAsync(
        string id,
        UpdateHolidayAndWorkingCalendarRequest request,
        IHolidayAndWorkingCalendarRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calendar id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var calendar = await repository.GetByIdAsync(id, cancellationToken);
        if (calendar is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(calendar);

        var updated = await repository.UpdateAsync(id, calendar, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("HolidayAndWorkingCalendar", calendar.CalendarCode, calendar.CalendarName, EventAction.Updated, cancellationToken);
        return Results.Ok(HolidayAndWorkingCalendarResponse.FromEntity(calendar));
    }

    private static async Task<IResult> DeleteHolidayAndWorkingCalendarAsync(
        string id,
        IHolidayAndWorkingCalendarRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calendar id." });
        }

        var calendar = await repository.GetByIdAsync(id, cancellationToken);
        if (calendar is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("HolidayAndWorkingCalendar", calendar.CalendarCode, calendar.CalendarName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
