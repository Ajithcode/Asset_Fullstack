using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class SignatureAndStampEndpoints
{
    private const string SequenceName = "signature_and_stamp";
    private const string SignatureCodePrefix = "SIG";

    public static RouteGroupBuilder MapSignatureAndStampEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/signatures-and-stamps").WithTags("SignaturesAndStamps");

        group.MapGet("", GetSignaturesAndStampsAsync).WithName("GetSignaturesAndStamps");
        group.MapGet("/{id}", GetSignatureAndStampByIdAsync).WithName("GetSignatureAndStampById");
        group.MapPost("", CreateSignatureAndStampAsync).WithName("CreateSignatureAndStamp");
        group.MapPut("/{id}", UpdateSignatureAndStampAsync).WithName("UpdateSignatureAndStamp");
        group.MapDelete("/{id}", DeleteSignatureAndStampAsync).WithName("DeleteSignatureAndStamp");

        return group;
    }

    private static async Task<IResult> GetSignaturesAndStampsAsync(
        ISignatureAndStampRepository repository,
        CancellationToken cancellationToken)
    {
        var signatures = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(signatures.Select(SignatureAndStampResponse.FromEntity));
    }

    private static async Task<IResult> GetSignatureAndStampByIdAsync(
        string id,
        ISignatureAndStampRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid signature id." });
        }

        var signature = await repository.GetByIdAsync(id, cancellationToken);
        if (signature is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("SignatureAndStamp", signature.SignatureCode, signature.SignatureName, EventAction.Viewed, cancellationToken);
        return Results.Ok(SignatureAndStampResponse.FromEntity(signature));
    }

    private static async Task<IResult> CreateSignatureAndStampAsync(
        CreateSignatureAndStampRequest request,
        ISignatureAndStampRepository repository,
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
        var signatureCode = $"{SignatureCodePrefix}{nextSequence:D6}";

        var signature = await repository.CreateAsync(request.ToEntity(signatureCode), cancellationToken);

        await eventLogger.LogAsync("SignatureAndStamp", signature.SignatureCode, signature.SignatureName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/signatures-and-stamps/{signature.Id}", SignatureAndStampResponse.FromEntity(signature));
    }

    private static async Task<IResult> UpdateSignatureAndStampAsync(
        string id,
        UpdateSignatureAndStampRequest request,
        ISignatureAndStampRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid signature id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var signature = await repository.GetByIdAsync(id, cancellationToken);
        if (signature is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(signature);

        var updated = await repository.UpdateAsync(id, signature, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("SignatureAndStamp", signature.SignatureCode, signature.SignatureName, EventAction.Updated, cancellationToken);
        return Results.Ok(SignatureAndStampResponse.FromEntity(signature));
    }

    private static async Task<IResult> DeleteSignatureAndStampAsync(
        string id,
        ISignatureAndStampRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid signature id." });
        }

        var signature = await repository.GetByIdAsync(id, cancellationToken);
        if (signature is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("SignatureAndStamp", signature.SignatureCode, signature.SignatureName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
