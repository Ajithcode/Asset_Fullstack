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

        group.MapPost("", CreateNumberingSequenceAsync).WithName("CreateNumberingSequence");

        return group;
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

        if (await repository.SequenceCodeExistsAsync(sequence.SequenceCode, sequence.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sequence code '{sequence.SequenceCode}' already exists" });
        }

        sequence = await repository.CreateAsync(sequence, cancellationToken);

        await eventLogger.LogAsync(EntityType, sequence.Id, sequence.SequenceName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/numbering-sequences/{sequence.SequenceId}",
            NumberingSequenceResponse.FromEntity(sequence));
    }
}
