using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class NumberingSequenceMappings
{
    public static NumberingSequence ToEntity(this CreateNumberingSequenceRequest request, CurrentUser? currentUser)
    {
        return new NumberingSequence
        {
            // Generated on the server so every sequence gets a stable id independent of the Mongo _id.
            SequenceId = Guid.NewGuid().ToString(),
            SequenceName = request.SequenceName!.Trim(),
            SequenceCode = request.SequenceCode!.Trim().ToUpperInvariant(),
            Module = request.Module!.Trim(),
            RecordType = request.RecordType!.Trim(),
            Prefix = request.Prefix!.Trim().ToUpperInvariant(),
            // Not trimmed: the separator is the literal character(s) placed between parts.
            Separator = request.Separator!,
            NumberLength = request.NumberLength!.Value,
            StartNumber = request.StartNumber!.Value,
            IncrementBy = request.IncrementBy!.Value,
            ResetFrequency = request.ResetFrequency!.Trim(),
            Scope = request.Scope!.Trim(),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    // Tenant, client, creator and the sequence id are kept from the stored sequence.
    public static void ApplyTo(this UpdateNumberingSequenceRequest request, NumberingSequence sequence, CurrentUser? currentUser)
    {
        sequence.SequenceName = request.SequenceName!.Trim();
        sequence.SequenceCode = request.SequenceCode!.Trim().ToUpperInvariant();
        sequence.Module = request.Module!.Trim();
        sequence.RecordType = request.RecordType!.Trim();
        sequence.Prefix = request.Prefix!.Trim().ToUpperInvariant();
        // Not trimmed: the separator is the literal character(s) placed between parts.
        sequence.Separator = request.Separator!;
        sequence.NumberLength = request.NumberLength!.Value;
        sequence.StartNumber = request.StartNumber!.Value;
        sequence.IncrementBy = request.IncrementBy!.Value;
        sequence.ResetFrequency = request.ResetFrequency!.Trim();
        sequence.Scope = request.Scope!.Trim();
        sequence.Status = request.Status!.Trim();
        sequence.UpdatedBy = currentUser?.UserName;
        sequence.UpdatedAt = DateTime.UtcNow;
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
