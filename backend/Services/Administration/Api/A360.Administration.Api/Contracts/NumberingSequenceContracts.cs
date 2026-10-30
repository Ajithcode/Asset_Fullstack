using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateNumberingSequenceRequest(
    string? SequenceName,
    string? SequenceCode,
    string? Module,
    string? RecordType,
    string? Prefix,
    string? Separator,
    int? NumberLength,
    long? StartNumber,
    int? IncrementBy,
    string? ResetFrequency,
    string? Scope,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record NumberingSequenceResponse(
    string SequenceId,
    string SequenceName,
    string SequenceCode,
    string Module,
    string RecordType,
    string Prefix,
    string Separator,
    int NumberLength,
    long StartNumber,
    int IncrementBy,
    string ResetFrequency,
    string Scope,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static NumberingSequenceResponse FromEntity(NumberingSequence sequence)
    {
        return new NumberingSequenceResponse(
            sequence.SequenceId,
            sequence.SequenceName,
            sequence.SequenceCode,
            sequence.Module,
            sequence.RecordType,
            sequence.Prefix,
            sequence.Separator,
            sequence.NumberLength,
            sequence.StartNumber,
            sequence.IncrementBy,
            sequence.ResetFrequency,
            sequence.Scope,
            sequence.Status,
            sequence.CreatedBy,
            sequence.CreatedAt,
            sequence.UpdatedBy,
            sequence.UpdatedAt,
            sequence.TenantId,
            sequence.ClientId);
    }
}
