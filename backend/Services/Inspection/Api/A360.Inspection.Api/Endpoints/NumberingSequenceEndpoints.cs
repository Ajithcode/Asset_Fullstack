using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class NumberingSequenceEndpoints
{
    private const string SequenceName = "numbering_sequence";
    private const string SequenceCodePrefix = "NS";

    public static RouteGroupBuilder MapNumberingSequenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/numbering-sequences").WithTags("NumberingSequences");

        group.MapGet("", GetNumberingSequencesAsync).WithName("GetNumberingSequences");
        group.MapGet("/{id}", GetNumberingSequenceByIdAsync).WithName("GetNumberingSequenceById");
        group.MapPost("", CreateNumberingSequenceAsync).WithName("CreateNumberingSequence");
        group.MapPut("/{id}", UpdateNumberingSequenceAsync).WithName("UpdateNumberingSequence");
        group.MapDelete("/{id}", DeleteNumberingSequenceAsync).WithName("DeleteNumberingSequence");

        return group;
    }

    private static async Task<IResult> GetNumberingSequencesAsync(
        INumberingSequenceRepository repository,
        CancellationToken cancellationToken)
    {
        var sequences = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(sequences.Select(NumberingSequenceResponse.FromEntity));
    }

    private static async Task<IResult> GetNumberingSequenceByIdAsync(
        string id,
        INumberingSequenceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid numbering sequence id." });
        }

        var sequence = await repository.GetByIdAsync(id, cancellationToken);
        if (sequence is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("NumberingSequence", sequence.SequenceCode, sequence.NumberType, EventAction.Viewed, cancellationToken);
        return Results.Ok(NumberingSequenceResponse.FromEntity(sequence));
    }

    private static async Task<IResult> CreateNumberingSequenceAsync(
        CreateNumberingSequenceRequest request,
        INumberingSequenceRepository repository,
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
        var sequenceCode = $"{SequenceCodePrefix}{nextSequence:D6}";

        var sequence = await repository.CreateAsync(request.ToEntity(sequenceCode), cancellationToken);

        await eventLogger.LogAsync("NumberingSequence", sequence.SequenceCode, sequence.NumberType, EventAction.Created, cancellationToken);

        return Results.Created($"/api/numbering-sequences/{sequence.Id}", NumberingSequenceResponse.FromEntity(sequence));
    }

    private static async Task<IResult> UpdateNumberingSequenceAsync(
        string id,
        UpdateNumberingSequenceRequest request,
        INumberingSequenceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid numbering sequence id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sequence = await repository.GetByIdAsync(id, cancellationToken);
        if (sequence is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(sequence);

        var updated = await repository.UpdateAsync(id, sequence, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("NumberingSequence", sequence.SequenceCode, sequence.NumberType, EventAction.Updated, cancellationToken);
        return Results.Ok(NumberingSequenceResponse.FromEntity(sequence));
    }

    private static async Task<IResult> DeleteNumberingSequenceAsync(
        string id,
        INumberingSequenceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid numbering sequence id." });
        }

        var sequence = await repository.GetByIdAsync(id, cancellationToken);
        if (sequence is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("NumberingSequence", sequence.SequenceCode, sequence.NumberType, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
