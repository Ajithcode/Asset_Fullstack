using A360.Inspection.Domain.Entities;
using CalibrationInstrumentEntity = A360.Inspection.Domain.Entities.CalibrationInstrument;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for CalibrationInstrument.</summary>
public abstract record CalibrationInstrumentFields
{
    public string? InstrumentName { get; init; }

    public string? SerialNumber { get; init; }

    public string? Location { get; init; }

    public string? Range { get; init; }

    public string? Unit { get; init; }

    public string? Tolerance { get; init; }

    public int? IntervalMonths { get; init; }

    public string? Provider { get; init; }

    public string? ProviderType { get; init; }

    public string? AssignedTo { get; init; }

    public string? LastCalibration { get; init; }

    public string? NextDue { get; init; }

    public string? Certificate { get; init; }

    public string? LastResult { get; init; }

    public List<CalibrationPoint>? Results { get; init; }

    public string? ReferenceStandard { get; init; }

    public string? ReferenceCertificate { get; init; }

    public bool? ValidAtCalibration { get; init; }

    public List<CalibrationHistoryEntry>? History { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(CalibrationInstrumentEntity instrument)
    {
        instrument.InstrumentName = InstrumentName ?? instrument.InstrumentName;
        instrument.SerialNumber = SerialNumber ?? instrument.SerialNumber;
        instrument.Location = Location ?? instrument.Location;
        instrument.Range = Range ?? instrument.Range;
        instrument.Unit = Unit ?? instrument.Unit;
        instrument.Tolerance = Tolerance ?? instrument.Tolerance;
        instrument.IntervalMonths = IntervalMonths ?? instrument.IntervalMonths;
        instrument.Provider = Provider ?? instrument.Provider;
        instrument.ProviderType = ProviderType ?? instrument.ProviderType;
        instrument.AssignedTo = AssignedTo ?? instrument.AssignedTo;
        instrument.LastCalibration = LastCalibration;
        instrument.NextDue = NextDue;
        instrument.Certificate = Certificate ?? instrument.Certificate;
        instrument.LastResult = LastResult ?? instrument.LastResult;
        instrument.Results = Results ?? instrument.Results;
        instrument.ReferenceStandard = ReferenceStandard ?? instrument.ReferenceStandard;
        instrument.ReferenceCertificate = ReferenceCertificate ?? instrument.ReferenceCertificate;
        instrument.ValidAtCalibration = ValidAtCalibration ?? instrument.ValidAtCalibration;
        instrument.History = History ?? instrument.History;
    }
}

public sealed record CreateCalibrationInstrumentRequest : CalibrationInstrumentFields
{
    public string? InstrumentCode { get; init; }

    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public CalibrationInstrumentEntity ToEntity()
    {
        var instrument = new CalibrationInstrumentEntity
        {
            InstrumentCode = (InstrumentCode ?? string.Empty).Trim(),
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(instrument);
        return instrument;
    }
}

public sealed record UpdateCalibrationInstrumentRequest : CalibrationInstrumentFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(CalibrationInstrumentEntity instrument)
    {
        ApplyFields(instrument);
        instrument.Status = Status ?? instrument.Status;
        instrument.UpdatedBy = UpdatedBy;
        instrument.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record CalibrationInstrumentResponse : AuditResponse
{
    private CalibrationInstrumentResponse(CalibrationInstrumentEntity instrument)
        : base(instrument)
    {
    }

    public string InstrumentCode { get; init; } = string.Empty;

    public string InstrumentName { get; init; } = default!;

    public string SerialNumber { get; init; } = default!;

    public string Location { get; init; } = default!;

    public string Range { get; init; } = default!;

    public string Unit { get; init; } = default!;

    public string Tolerance { get; init; } = default!;

    public int IntervalMonths { get; init; }

    public string Provider { get; init; } = default!;

    public string ProviderType { get; init; } = default!;

    public string AssignedTo { get; init; } = default!;

    public string? LastCalibration { get; init; }

    public string? NextDue { get; init; }

    public string Certificate { get; init; } = default!;

    public string LastResult { get; init; } = default!;

    public List<CalibrationPoint> Results { get; init; } = default!;

    public string ReferenceStandard { get; init; } = default!;

    public string ReferenceCertificate { get; init; } = default!;

    public bool ValidAtCalibration { get; init; }

    public List<CalibrationHistoryEntry> History { get; init; } = default!;

    public static CalibrationInstrumentResponse FromEntity(CalibrationInstrumentEntity instrument)
    {
        return new CalibrationInstrumentResponse(instrument)
        {
            InstrumentCode = instrument.InstrumentCode,
            InstrumentName = instrument.InstrumentName,
            SerialNumber = instrument.SerialNumber,
            Location = instrument.Location,
            Range = instrument.Range,
            Unit = instrument.Unit,
            Tolerance = instrument.Tolerance,
            IntervalMonths = instrument.IntervalMonths,
            Provider = instrument.Provider,
            ProviderType = instrument.ProviderType,
            AssignedTo = instrument.AssignedTo,
            LastCalibration = instrument.LastCalibration,
            NextDue = instrument.NextDue,
            Certificate = instrument.Certificate,
            LastResult = instrument.LastResult,
            Results = instrument.Results,
            ReferenceStandard = instrument.ReferenceStandard,
            ReferenceCertificate = instrument.ReferenceCertificate,
            ValidAtCalibration = instrument.ValidAtCalibration,
            History = instrument.History
        };
    }
}
