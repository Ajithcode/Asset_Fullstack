using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ShiftEndpoints
{
    public static RouteGroupBuilder MapShiftEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/shifts")
            .WithTags("Shifts");

        group.MapGet("", GetShiftsAsync)
            .WithName("GetShifts");

        group.MapGet("/{id}", GetShiftByIdAsync)
            .WithName("GetShiftById");

        group.MapPost("", CreateShiftAsync)
            .WithName("CreateShift");

        group.MapPut("/{id}", UpdateShiftAsync)
            .WithName("UpdateShift");

        group.MapPatch("/{id}/status", UpdateShiftStatusAsync)
            .WithName("UpdateShiftStatus");

        group.MapDelete("/{id}", DeleteShiftAsync)
            .WithName("DeleteShift");

        return group;
    }

    private static async Task<IResult> GetShiftsAsync(
        string? search,
        string? status,
        string? shiftTime,
        string? workingDay,
        string? clientId,
        IShiftRepository repository,
        CancellationToken cancellationToken)
    {
        // Days are stored as full names, so "mon" or "monday" is turned into "Monday" first.
        string? day = null;

        if (!string.IsNullOrWhiteSpace(workingDay))
        {
            if (!ShiftTimes.TryParseDay(workingDay, out var parsed))
            {
                return Results.BadRequest(
                    new { message = $"Unknown working day '{workingDay}'." });
            }

            day = parsed.ToString();
        }

        var shifts = await repository.SearchAsync(
            new ShiftFilter(search, status, shiftTime, day, clientId),
            cancellationToken);

        return Results.Ok(
            shifts.Select(ShiftResponse.FromEntity));
    }

    private static async Task<IResult> GetShiftByIdAsync(
        string id,
        IShiftRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid shift id." });
        }

        var shift = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (shift is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Shift",
            shift.Id,
            shift.ShiftName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ShiftResponse.FromEntity(shift));
    }

    private static async Task<IResult> CreateShiftAsync(
        CreateShiftRequest request,
        IShiftRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var shift = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            shift.Code,
            shift.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A shift with code '{shift.Code}' already exists." });
        }

        shift = await repository.CreateAsync(
            shift,
            cancellationToken);

        await eventLogger.LogAsync(
            "Shift",
            shift.Id,
            shift.ShiftName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/shifts/{shift.Id}",
            ShiftResponse.FromEntity(shift));
    }

    private static async Task<IResult> UpdateShiftAsync(
        string id,
        UpdateShiftRequest request,
        IShiftRepository repository,
        ITeamRepository teams,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid shift id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var shift = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (shift is null)
        {
            return Results.NotFound();
        }

        var previousName = shift.ShiftName;

        request.ApplyTo(shift);

        var existing = await repository.GetByCodeAsync(
            shift.Code,
            shift.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A shift with code '{shift.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            shift,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != shift.ShiftName)
        {
            await teams.UpdateShiftNameAsync(
                id,
                shift.ShiftName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Shift",
            shift.Id,
            shift.ShiftName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ShiftResponse.FromEntity(shift));
    }

    private static async Task<IResult> UpdateShiftStatusAsync(
        string id,
        UpdateShiftStatusRequest request,
        IShiftRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid shift id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var shift = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (shift is null)
        {
            return Results.NotFound();
        }

        shift.Status = request.Status;
        shift.UpdatedBy = request.UpdatedBy;
        shift.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            shift,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Shift",
            shift.Id,
            shift.ShiftName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ShiftResponse.FromEntity(shift));
    }

    private static async Task<IResult> DeleteShiftAsync(
        string id,
        IShiftRepository repository,
        ITeamRepository teams,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid shift id." });
        }

        var shift = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (shift is null)
        {
            return Results.NotFound();
        }

        if (await teams.AnyWithShiftAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Teams are still on this shift. Move them to another shift first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Shift",
            shift.Id,
            shift.ShiftName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
