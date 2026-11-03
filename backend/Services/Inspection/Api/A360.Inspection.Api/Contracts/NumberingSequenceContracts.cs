using NumberingSequenceEntity = A360.Inspection.Domain.Entities.NumberingSequence;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateNumberingSequenceRequest(
    string? NumberType,
    string? Prefix,
    string? Suffix,
    string? FinancialYear,
    string? SiteCode,
    string? DepartmentCode,
    int? RunningNumber,
    string? ResetFrequency,
    string? SampleNumberPreview,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public NumberingSequenceEntity ToEntity(string sequenceCode)
    {
        return new NumberingSequenceEntity
        {
            SequenceCode = sequenceCode,
            NumberType = NumberType ?? string.Empty,
            Prefix = Prefix ?? string.Empty,
            Suffix = Suffix ?? string.Empty,
            FinancialYear = FinancialYear ?? string.Empty,
            SiteCode = SiteCode ?? string.Empty,
            DepartmentCode = DepartmentCode ?? string.Empty,
            RunningNumber = RunningNumber ?? 0,
            ResetFrequency = ResetFrequency ?? string.Empty,
            SampleNumberPreview = SampleNumberPreview ?? string.Empty,
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateNumberingSequenceRequest(
    string? NumberType,
    string? Prefix,
    string? Suffix,
    string? FinancialYear,
    string? SiteCode,
    string? DepartmentCode,
    int? RunningNumber,
    string? ResetFrequency,
    string? SampleNumberPreview,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(NumberingSequenceEntity sequence)
    {
        sequence.NumberType = NumberType ?? string.Empty;
        sequence.Prefix = Prefix ?? string.Empty;
        sequence.Suffix = Suffix ?? string.Empty;
        sequence.FinancialYear = FinancialYear ?? string.Empty;
        sequence.SiteCode = SiteCode ?? string.Empty;
        sequence.DepartmentCode = DepartmentCode ?? string.Empty;
        sequence.RunningNumber = RunningNumber ?? sequence.RunningNumber;
        sequence.ResetFrequency = ResetFrequency ?? string.Empty;
        sequence.SampleNumberPreview = SampleNumberPreview ?? string.Empty;
        sequence.IsActive = IsActive ?? sequence.IsActive;
        sequence.Status = Status;
        sequence.UpdatedBy = UpdatedBy;
        sequence.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record NumberingSequenceResponse(
    string Id,
    string SequenceCode,
    string NumberType,
    string Prefix,
    string Suffix,
    string FinancialYear,
    string SiteCode,
    string DepartmentCode,
    int RunningNumber,
    string ResetFrequency,
    string SampleNumberPreview,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static NumberingSequenceResponse FromEntity(NumberingSequenceEntity sequence)
    {
        return new NumberingSequenceResponse(
            sequence.Id,
            sequence.SequenceCode,
            sequence.NumberType,
            sequence.Prefix,
            sequence.Suffix,
            sequence.FinancialYear,
            sequence.SiteCode,
            sequence.DepartmentCode,
            sequence.RunningNumber,
            sequence.ResetFrequency,
            sequence.SampleNumberPreview,
            sequence.IsActive,
            sequence.Status,
            sequence.CreatedBy,
            sequence.CreatedAt,
            sequence.UpdatedBy,
            sequence.UpdatedAt,
            sequence.ClientId,
            sequence.TenantId,
            sequence.IsDeleted);
    }
}
