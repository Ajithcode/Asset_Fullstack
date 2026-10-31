using A360.Inspection.Domain.Entities;
using NumberingSequenceEntity = A360.Inspection.Domain.Entities.NumberingSequence;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for NumberingSequence.</summary>
public abstract record NumberingSequenceFields
{
    public string? SequenceName { get; init; }

    public string? NumberType { get; init; }

    public string? Prefix { get; init; }

    public string? Separator { get; init; }

    public string? YearFormat { get; init; }

    public int? SequenceDigits { get; init; }

    public int? StartingNumber { get; init; }

    public int? RunningNumber { get; init; }

    public string? ResetFrequency { get; init; }

    public string? Scope { get; init; }

    public string? Timezone { get; init; }

    public string? Format { get; init; }

    public bool? GenerateOnFinalCreation { get; init; }

    public bool? DoNotReuse { get; init; }

    public bool? AllowManualOverride { get; init; }

    public string? LastAllocated { get; init; }

    public int? AssignedThisYear { get; init; }

    public List<SequenceAllocation>? Allocations { get; init; }

    public bool? IsActive { get; init; }

    public string? Suffix { get; init; }

    public string? FinancialYear { get; init; }

    public string? SiteCode { get; init; }

    public string? DepartmentCode { get; init; }

    public string? SampleNumberPreview { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(NumberingSequenceEntity sequence)
    {
        sequence.SequenceName = SequenceName ?? sequence.SequenceName;
        sequence.NumberType = NumberType ?? sequence.NumberType;
        sequence.Prefix = Prefix ?? sequence.Prefix;
        sequence.Separator = Separator ?? sequence.Separator;
        sequence.YearFormat = YearFormat ?? sequence.YearFormat;
        sequence.SequenceDigits = SequenceDigits ?? sequence.SequenceDigits;
        sequence.StartingNumber = StartingNumber ?? sequence.StartingNumber;
        sequence.RunningNumber = RunningNumber ?? sequence.RunningNumber;
        sequence.ResetFrequency = ResetFrequency ?? sequence.ResetFrequency;
        sequence.Scope = Scope ?? sequence.Scope;
        sequence.Timezone = Timezone ?? sequence.Timezone;
        sequence.Format = Format ?? sequence.Format;
        sequence.GenerateOnFinalCreation = GenerateOnFinalCreation ?? sequence.GenerateOnFinalCreation;
        sequence.DoNotReuse = DoNotReuse ?? sequence.DoNotReuse;
        sequence.AllowManualOverride = AllowManualOverride ?? sequence.AllowManualOverride;
        sequence.LastAllocated = LastAllocated ?? sequence.LastAllocated;
        sequence.AssignedThisYear = AssignedThisYear ?? sequence.AssignedThisYear;
        sequence.Allocations = Allocations ?? sequence.Allocations;
        sequence.IsActive = IsActive ?? sequence.IsActive;
        sequence.Suffix = Suffix ?? sequence.Suffix;
        sequence.FinancialYear = FinancialYear ?? sequence.FinancialYear;
        sequence.SiteCode = SiteCode ?? sequence.SiteCode;
        sequence.DepartmentCode = DepartmentCode ?? sequence.DepartmentCode;
        sequence.SampleNumberPreview = SampleNumberPreview ?? sequence.SampleNumberPreview;
    }
}

public sealed record CreateNumberingSequenceRequest : NumberingSequenceFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public NumberingSequenceEntity ToEntity(string sequenceCode)
    {
        var sequence = new NumberingSequenceEntity
        {
            SequenceCode = sequenceCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(sequence);
        return sequence;
    }
}

public sealed record UpdateNumberingSequenceRequest : NumberingSequenceFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(NumberingSequenceEntity sequence)
    {
        ApplyFields(sequence);
        sequence.Status = Status ?? sequence.Status;
        sequence.UpdatedBy = UpdatedBy;
        sequence.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record NumberingSequenceResponse : AuditResponse
{
    private NumberingSequenceResponse(NumberingSequenceEntity sequence)
        : base(sequence)
    {
    }

    public string SequenceCode { get; init; } = string.Empty;

    public string SequenceName { get; init; } = default!;

    public string NumberType { get; init; } = default!;

    public string Prefix { get; init; } = default!;

    public string Separator { get; init; } = default!;

    public string YearFormat { get; init; } = default!;

    public int SequenceDigits { get; init; }

    public int StartingNumber { get; init; }

    public int RunningNumber { get; init; }

    public string ResetFrequency { get; init; } = default!;

    public string Scope { get; init; } = default!;

    public string Timezone { get; init; } = default!;

    public string Format { get; init; } = default!;

    public bool GenerateOnFinalCreation { get; init; }

    public bool DoNotReuse { get; init; }

    public bool AllowManualOverride { get; init; }

    public string LastAllocated { get; init; } = default!;

    public int AssignedThisYear { get; init; }

    public List<SequenceAllocation> Allocations { get; init; } = default!;

    public bool IsActive { get; init; }

    public string Suffix { get; init; } = default!;

    public string FinancialYear { get; init; } = default!;

    public string SiteCode { get; init; } = default!;

    public string DepartmentCode { get; init; } = default!;

    public string SampleNumberPreview { get; init; } = default!;

    public static NumberingSequenceResponse FromEntity(NumberingSequenceEntity sequence)
    {
        return new NumberingSequenceResponse(sequence)
        {
            SequenceCode = sequence.SequenceCode,
            SequenceName = sequence.SequenceName,
            NumberType = sequence.NumberType,
            Prefix = sequence.Prefix,
            Separator = sequence.Separator,
            YearFormat = sequence.YearFormat,
            SequenceDigits = sequence.SequenceDigits,
            StartingNumber = sequence.StartingNumber,
            RunningNumber = sequence.RunningNumber,
            ResetFrequency = sequence.ResetFrequency,
            Scope = sequence.Scope,
            Timezone = sequence.Timezone,
            Format = sequence.Format,
            GenerateOnFinalCreation = sequence.GenerateOnFinalCreation,
            DoNotReuse = sequence.DoNotReuse,
            AllowManualOverride = sequence.AllowManualOverride,
            LastAllocated = sequence.LastAllocated,
            AssignedThisYear = sequence.AssignedThisYear,
            Allocations = sequence.Allocations,
            IsActive = sequence.IsActive,
            Suffix = sequence.Suffix,
            FinancialYear = sequence.FinancialYear,
            SiteCode = sequence.SiteCode,
            DepartmentCode = sequence.DepartmentCode,
            SampleNumberPreview = sequence.SampleNumberPreview
        };
    }
}
