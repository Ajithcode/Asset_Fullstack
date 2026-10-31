using A360.Inspection.Domain.Entities;
using SignatureAndStampEntity = A360.Inspection.Domain.Entities.SignatureAndStamp;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for SignatureAndStamp.</summary>
public abstract record SignatureAndStampFields
{
    public string? Kind { get; init; }

    public string? User { get; init; }

    public string? SignatureName { get; init; }

    public string? Role { get; init; }

    public string? Department { get; init; }

    public string? AuthorizedRole { get; init; }

    public string? SignatureImage { get; init; }

    public string? StampImage { get; init; }

    public string? AssignedStampCode { get; init; }

    public string? EffectiveDate { get; init; }

    public string? ExpiryDate { get; init; }

    public string? SignaturePosition { get; init; }

    public string? StampPosition { get; init; }

    public bool? IncludeSignerNameDate { get; init; }

    public bool? RequirePasswordConfirmationBeforeSigning { get; init; }

    public bool? IsActive { get; init; }

    public string? DigitalSignatureCertificate { get; init; }

    public bool? IsDefaultSignature { get; init; }

    public bool? IsDefaultStamp { get; init; }

    public bool? AllowManualSignature { get; init; }

    public bool? AllowUploadedSignature { get; init; }

    public bool? AllowBoth { get; init; }

    public bool? RequireOtpConfirmation { get; init; }

    public List<SignatureUsageLogEntry>? SignatureUsageLog { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(SignatureAndStampEntity signature)
    {
        signature.Kind = Kind ?? signature.Kind;
        signature.User = User ?? signature.User;
        signature.SignatureName = SignatureName ?? signature.SignatureName;
        signature.Role = Role ?? signature.Role;
        signature.Department = Department ?? signature.Department;
        signature.AuthorizedRole = AuthorizedRole ?? signature.AuthorizedRole;
        signature.SignatureImage = SignatureImage ?? signature.SignatureImage;
        signature.StampImage = StampImage ?? signature.StampImage;
        signature.AssignedStampCode = AssignedStampCode ?? signature.AssignedStampCode;
        signature.EffectiveDate = ContractDates.ParseDate(EffectiveDate);
        signature.ExpiryDate = ContractDates.ParseDate(ExpiryDate);
        signature.SignaturePosition = SignaturePosition ?? signature.SignaturePosition;
        signature.StampPosition = StampPosition ?? signature.StampPosition;
        signature.IncludeSignerNameDate = IncludeSignerNameDate ?? signature.IncludeSignerNameDate;
        signature.RequirePasswordConfirmationBeforeSigning = RequirePasswordConfirmationBeforeSigning ?? signature.RequirePasswordConfirmationBeforeSigning;
        signature.IsActive = IsActive ?? signature.IsActive;
        signature.DigitalSignatureCertificate = DigitalSignatureCertificate ?? signature.DigitalSignatureCertificate;
        signature.IsDefaultSignature = IsDefaultSignature ?? signature.IsDefaultSignature;
        signature.IsDefaultStamp = IsDefaultStamp ?? signature.IsDefaultStamp;
        signature.AllowManualSignature = AllowManualSignature ?? signature.AllowManualSignature;
        signature.AllowUploadedSignature = AllowUploadedSignature ?? signature.AllowUploadedSignature;
        signature.AllowBoth = AllowBoth ?? signature.AllowBoth;
        signature.RequireOtpConfirmation = RequireOtpConfirmation ?? signature.RequireOtpConfirmation;
        signature.SignatureUsageLog = SignatureUsageLog ?? signature.SignatureUsageLog;

        if (string.IsNullOrWhiteSpace(signature.Kind))
        {
            signature.Kind = SignatureAndStampKinds.Signature;
        }
    }
}

public sealed record CreateSignatureAndStampRequest : SignatureAndStampFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public SignatureAndStampEntity ToEntity(string signatureCode)
    {
        var signature = new SignatureAndStampEntity
        {
            SignatureCode = signatureCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(signature);
        return signature;
    }
}

public sealed record UpdateSignatureAndStampRequest : SignatureAndStampFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(SignatureAndStampEntity signature)
    {
        ApplyFields(signature);
        signature.Status = Status ?? signature.Status;
        signature.UpdatedBy = UpdatedBy;
        signature.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record SignatureAndStampResponse : AuditResponse
{
    private SignatureAndStampResponse(SignatureAndStampEntity signature)
        : base(signature)
    {
    }

    public string SignatureCode { get; init; } = string.Empty;

    public string Kind { get; init; } = default!;

    public string User { get; init; } = default!;

    public string SignatureName { get; init; } = default!;

    public string Role { get; init; } = default!;

    public string Department { get; init; } = default!;

    public string AuthorizedRole { get; init; } = default!;

    public string SignatureImage { get; init; } = default!;

    public string StampImage { get; init; } = default!;

    public string AssignedStampCode { get; init; } = default!;

    public string? EffectiveDate { get; init; }

    public string? ExpiryDate { get; init; }

    public string SignaturePosition { get; init; } = default!;

    public string StampPosition { get; init; } = default!;

    public bool IncludeSignerNameDate { get; init; }

    public bool RequirePasswordConfirmationBeforeSigning { get; init; }

    public bool IsActive { get; init; }

    public string DigitalSignatureCertificate { get; init; } = default!;

    public bool IsDefaultSignature { get; init; }

    public bool IsDefaultStamp { get; init; }

    public bool AllowManualSignature { get; init; }

    public bool AllowUploadedSignature { get; init; }

    public bool AllowBoth { get; init; }

    public bool RequireOtpConfirmation { get; init; }

    public List<SignatureUsageLogEntry> SignatureUsageLog { get; init; } = default!;

    public static SignatureAndStampResponse FromEntity(SignatureAndStampEntity signature)
    {
        return new SignatureAndStampResponse(signature)
        {
            SignatureCode = signature.SignatureCode,
            Kind = string.IsNullOrWhiteSpace(signature.Kind) ? SignatureAndStampKinds.Signature : signature.Kind,
            User = signature.User,
            SignatureName = signature.SignatureName,
            Role = signature.Role,
            Department = signature.Department,
            AuthorizedRole = signature.AuthorizedRole,
            SignatureImage = signature.SignatureImage,
            StampImage = signature.StampImage,
            AssignedStampCode = signature.AssignedStampCode,
            EffectiveDate = ContractDates.ToDateString(signature.EffectiveDate),
            ExpiryDate = ContractDates.ToDateString(signature.ExpiryDate),
            SignaturePosition = signature.SignaturePosition,
            StampPosition = signature.StampPosition,
            IncludeSignerNameDate = signature.IncludeSignerNameDate,
            RequirePasswordConfirmationBeforeSigning = signature.RequirePasswordConfirmationBeforeSigning,
            IsActive = signature.IsActive,
            DigitalSignatureCertificate = signature.DigitalSignatureCertificate,
            IsDefaultSignature = signature.IsDefaultSignature,
            IsDefaultStamp = signature.IsDefaultStamp,
            AllowManualSignature = signature.AllowManualSignature,
            AllowUploadedSignature = signature.AllowUploadedSignature,
            AllowBoth = signature.AllowBoth,
            RequireOtpConfirmation = signature.RequireOtpConfirmation,
            SignatureUsageLog = signature.SignatureUsageLog
        };
    }
}
