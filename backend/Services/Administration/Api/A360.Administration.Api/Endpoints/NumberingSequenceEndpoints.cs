using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class NumberingSequenceEndpoints
{
    private const string EntityType = "NumberingSequence";

    public static RouteGroupBuilder MapNumberingSequenceEndpoints(this IEndpointRouteBuilder routes)
    {
        // Prefixed with /administration because the Inspection service already owns /api/numbering-sequences at the gateway.
        var group = routes.MapGroup("/api/administration/numbering-sequences").WithTags("Numbering Sequences");

        group.MapGet("", GetNumberingSequencesAsync).WithName("GetNumberingSequences");
        group.MapGet("/{sequenceId}", GetNumberingSequenceByIdAsync).WithName("GetNumberingSequenceById");
        group.MapPost("", CreateNumberingSequenceAsync).WithName("CreateNumberingSequence");
        group.MapPut("/{sequenceId}", UpdateNumberingSequenceAsync).WithName("UpdateNumberingSequence");
        group.MapDelete("/{sequenceId}", DeleteNumberingSequenceAsync).WithName("DeleteNumberingSequence");

        return group;
    }

    private static async Task<IResult> GetNumberingSequencesAsync(
        string? tenantId,
        string? clientId,
        string? module,
        string? status,
        INumberingSequenceRepository repository,
        CancellationToken cancellationToken)
    {
        var sequences = await repository.GetAsync(tenantId, clientId, module, status, cancellationToken);

        return Results.Ok(sequences.Select(NumberingSequenceResponse.FromEntity));
    }

    private static async Task<IResult> GetNumberingSequenceByIdAsync(
        string sequenceId,
        INumberingSequenceRepository repository,
        CancellationToken cancellationToken)
    {
        var sequence = await repository.GetBySequenceIdAsync(sequenceId, cancellationToken);

        return sequence is null ? NotFound(sequenceId) : Results.Ok(NumberingSequenceResponse.FromEntity(sequence));
    }

    private static async Task<IResult> CreateNumberingSequenceAsync(
        CreateNumberingSequenceRequest request,
        INumberingSequenceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sequence = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.SequenceCodeExistsAsync(sequence.SequenceCode, sequence.TenantId, null, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sequence code '{sequence.SequenceCode}' already exists" });
        }

        sequence = await repository.CreateAsync(sequence, cancellationToken);

        await eventLogger.LogAsync(EntityType, sequence.Id, sequence.SequenceName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/numbering-sequences/{sequence.SequenceId}",
            NumberingSequenceResponse.FromEntity(sequence));
    }

    private static async Task<IResult> UpdateNumberingSequenceAsync(
        string sequenceId,
        UpdateNumberingSequenceRequest request,
        INumberingSequenceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sequence = await repository.GetBySequenceIdAsync(sequenceId, cancellationToken);
        if (sequence is null)
        {
            return NotFound(sequenceId);
        }

        request.ApplyTo(sequence, currentUserAccessor.GetCurrentUser());

        if (await repository.SequenceCodeExistsAsync(sequence.SequenceCode, sequence.TenantId, sequence.SequenceId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sequence code '{sequence.SequenceCode}' already exists" });
        }

        if (!await repository.UpdateAsync(sequence.Id, sequence, cancellationToken))
        {
            return NotFound(sequenceId);
        }

        await eventLogger.LogAsync(EntityType, sequence.Id, sequence.SequenceName, EventAction.Updated, cancellationToken);

        return Results.Ok(NumberingSequenceResponse.FromEntity(sequence));
    }

    // Soft delete: the record is kept with IsDeleted = true so its history stays available.
    private static async Task<IResult> DeleteNumberingSequenceAsync(
        string sequenceId,
        INumberingSequenceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var sequence = await repository.GetBySequenceIdAsync(sequenceId, cancellationToken);
        if (sequence is null)
        {
            return NotFound(sequenceId);
        }

        sequence.IsDeleted = true;
        sequence.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        sequence.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(sequence.Id, sequence, cancellationToken))
        {
            return NotFound(sequenceId);
        }

        await eventLogger.LogAsync(EntityType, sequence.Id, sequence.SequenceName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string sequenceId)
    {
        return Results.NotFound(new { message = $"Numbering sequence '{sequenceId}' was not found" });
    }
}
