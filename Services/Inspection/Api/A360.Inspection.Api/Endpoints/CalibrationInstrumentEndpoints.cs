using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class CalibrationInstrumentEndpoints
{

    public static RouteGroupBuilder MapCalibrationInstrumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/calibration-instruments").WithTags("CalibrationInstruments");

        group.MapGet("", GetCalibrationInstrumentsAsync).WithName("GetCalibrationInstruments");
        group.MapGet("/{id}", GetCalibrationInstrumentByIdAsync).WithName("GetCalibrationInstrumentById");
        group.MapPost("", CreateCalibrationInstrumentAsync).WithName("CreateCalibrationInstrument");
        group.MapPut("/{id}", UpdateCalibrationInstrumentAsync).WithName("UpdateCalibrationInstrument");
        group.MapDelete("/{id}", DeleteCalibrationInstrumentAsync).WithName("DeleteCalibrationInstrument");

        return group;
    }

    private static async Task<IResult> GetCalibrationInstrumentsAsync(
        ICalibrationInstrumentRepository repository,
        CancellationToken cancellationToken)
    {
        var instruments = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(instruments.Select(CalibrationInstrumentResponse.FromEntity));
    }

    private static async Task<IResult> GetCalibrationInstrumentByIdAsync(
        string id,
        ICalibrationInstrumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calibration instrument id." });
        }

        var instrument = await repository.GetByIdAsync(id, cancellationToken);
        if (instrument is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("CalibrationInstrument", instrument.InstrumentCode, instrument.InstrumentName, EventAction.Viewed, cancellationToken);
        return Results.Ok(CalibrationInstrumentResponse.FromEntity(instrument));
    }

    private static async Task<IResult> CreateCalibrationInstrumentAsync(
        CreateCalibrationInstrumentRequest request,
        ICalibrationInstrumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        // The instrument code (e.g. PG-001) is entered by the user, so uniqueness is checked here.
        var entity = request.ToEntity();
        var existing = await repository.GetByInstrumentCodeAsync(entity.InstrumentCode, cancellationToken);
        if (existing is not null)
        {
            return Results.Conflict(new { message = $"Instrument code '{entity.InstrumentCode}' already exists." });
        }

        var instrument = await repository.CreateAsync(entity, cancellationToken);

        await eventLogger.LogAsync("CalibrationInstrument", instrument.InstrumentCode, instrument.InstrumentName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/calibration-instruments/{instrument.Id}", CalibrationInstrumentResponse.FromEntity(instrument));
    }

    private static async Task<IResult> UpdateCalibrationInstrumentAsync(
        string id,
        UpdateCalibrationInstrumentRequest request,
        ICalibrationInstrumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calibration instrument id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var instrument = await repository.GetByIdAsync(id, cancellationToken);
        if (instrument is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(instrument);

        var updated = await repository.UpdateAsync(id, instrument, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("CalibrationInstrument", instrument.InstrumentCode, instrument.InstrumentName, EventAction.Updated, cancellationToken);
        return Results.Ok(CalibrationInstrumentResponse.FromEntity(instrument));
    }

    private static async Task<IResult> DeleteCalibrationInstrumentAsync(
        string id,
        ICalibrationInstrumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid calibration instrument id." });
        }

        var instrument = await repository.GetByIdAsync(id, cancellationToken);
        if (instrument is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("CalibrationInstrument", instrument.InstrumentCode, instrument.InstrumentName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
