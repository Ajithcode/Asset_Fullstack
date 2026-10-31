using SignatureAndStampEntity = A360.Inspection.Domain.Entities.SignatureAndStamp;
using SignatureUsageLogEntryEntity = A360.Inspection.Domain.Entities.SignatureUsageLogEntry;

namespace A360.Inspection.Api.Contracts;

public sealed record SignatureUsageLogEntryDto(
    string? IpAddress,
    string? DeviceInformation,
    DateTime? Timestamp,
    string? SignatureHash)
{
    public SignatureUsageLogEntryEntity ToEntity()
    {
        return new SignatureUsageLogEntryEntity
        {
            IpAddress = IpAddress ?? string.Empty,
            DeviceInformation = DeviceInformation ?? string.Empty,
            Timestamp = Timestamp ?? DateTime.UtcNow,
            SignatureHash = SignatureHash ?? string.Empty
        };
    }

    public static SignatureUsageLogEntryDto FromEntity(SignatureUsageLogEntryEntity entry)
    {
        return new SignatureUsageLogEntryDto(entry.IpAddress, entry.DeviceInformation, entry.Timestamp, entry.SignatureHash);
    }
}

public sealed record CreateSignatureAndStampRequest(
    string? User,
    string? SignatureName,
    string? SignatureImage,
    string? StampImage,
    string? DigitalSignatureCertificate,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    bool? IsDefaultSignature,
    bool? IsDefaultStamp,
    bool? AllowManualSignature,
    bool? AllowUploadedSignature,
    bool? AllowBoth,
    bool? IsActive,
    string? Status,
    bool? RequirePasswordConfirmationBeforeSigning,
    bool? RequireOtpConfirmation,
    List<SignatureUsageLogEntryDto>? SignatureUsageLog,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SignatureAndStampEntity ToEntity(string signatureCode)
    {
        return new SignatureAndStampEntity
        {
            SignatureCode = signatureCode,
            User = User ?? string.Empty,
            SignatureName = SignatureName ?? string.Empty,
            SignatureImage = SignatureImage ?? string.Empty,
            StampImage = StampImage ?? string.Empty,
            DigitalSignatureCertificate = DigitalSignatureCertificate ?? string.Empty,
            EffectiveDate = EffectiveDate,
            ExpiryDate = ExpiryDate,
            IsDefaultSignature = IsDefaultSignature ?? false,
            IsDefaultStamp = IsDefaultStamp ?? false,
            AllowManualSignature = AllowManualSignature ?? false,
            AllowUploadedSignature = AllowUploadedSignature ?? false,
            AllowBoth = AllowBoth ?? false,
            IsActive = IsActive ?? true,
            Status = Status,
            RequirePasswordConfirmationBeforeSigning = RequirePasswordConfirmationBeforeSigning ?? false,
            RequireOtpConfirmation = RequireOtpConfirmation ?? false,
            SignatureUsageLog = SignatureUsageLog?.Select(entry => entry.ToEntity()).ToList() ?? [],
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSignatureAndStampRequest(
    string? User,
    string? SignatureName,
    string? SignatureImage,
    string? StampImage,
    string? DigitalSignatureCertificate,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    bool? IsDefaultSignature,
    bool? IsDefaultStamp,
    bool? AllowManualSignature,
    bool? AllowUploadedSignature,
    bool? AllowBoth,
    bool? IsActive,
    string? Status,
    bool? RequirePasswordConfirmationBeforeSigning,
    bool? RequireOtpConfirmation,
    List<SignatureUsageLogEntryDto>? SignatureUsageLog,
    string? UpdatedBy)
{
    public void ApplyTo(SignatureAndStampEntity signature)
    {
        signature.User = User ?? string.Empty;
        signature.SignatureName = SignatureName ?? string.Empty;
        signature.SignatureImage = SignatureImage ?? string.Empty;
        signature.StampImage = StampImage ?? string.Empty;
        signature.DigitalSignatureCertificate = DigitalSignatureCertificate ?? string.Empty;
        signature.EffectiveDate = EffectiveDate;
        signature.ExpiryDate = ExpiryDate;
        signature.IsDefaultSignature = IsDefaultSignature ?? signature.IsDefaultSignature;
        signature.IsDefaultStamp = IsDefaultStamp ?? signature.IsDefaultStamp;
        signature.AllowManualSignature = AllowManualSignature ?? signature.AllowManualSignature;
        signature.AllowUploadedSignature = AllowUploadedSignature ?? signature.AllowUploadedSignature;
        signature.AllowBoth = AllowBoth ?? signature.AllowBoth;
        signature.IsActive = IsActive ?? signature.IsActive;
        signature.Status = Status;
        signature.RequirePasswordConfirmationBeforeSigning = RequirePasswordConfirmationBeforeSigning ?? signature.RequirePasswordConfirmationBeforeSigning;
        signature.RequireOtpConfirmation = RequireOtpConfirmation ?? signature.RequireOtpConfirmation;
        if (SignatureUsageLog is not null)
        {
            signature.SignatureUsageLog = SignatureUsageLog.Select(entry => entry.ToEntity()).ToList();
        }
        signature.UpdatedBy = UpdatedBy;
        signature.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record SignatureAndStampResponse(
    string Id,
    string SignatureCode,
    string User,
    string SignatureName,
    string SignatureImage,
    string StampImage,
    string DigitalSignatureCertificate,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    bool IsDefaultSignature,
    bool IsDefaultStamp,
    bool AllowManualSignature,
    bool AllowUploadedSignature,
    bool AllowBoth,
    bool IsActive,
    string? Status,
    bool RequirePasswordConfirmationBeforeSigning,
    bool RequireOtpConfirmation,
    List<SignatureUsageLogEntryDto> SignatureUsageLog,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static SignatureAndStampResponse FromEntity(SignatureAndStampEntity signature)
    {
        return new SignatureAndStampResponse(
            signature.Id,
            signature.SignatureCode,
            signature.User,
            signature.SignatureName,
            signature.SignatureImage,
            signature.StampImage,
            signature.DigitalSignatureCertificate,
            signature.EffectiveDate,
            signature.ExpiryDate,
            signature.IsDefaultSignature,
            signature.IsDefaultStamp,
            signature.AllowManualSignature,
            signature.AllowUploadedSignature,
            signature.AllowBoth,
            signature.IsActive,
            signature.Status,
            signature.RequirePasswordConfirmationBeforeSigning,
            signature.RequireOtpConfirmation,
            signature.SignatureUsageLog.Select(SignatureUsageLogEntryDto.FromEntity).ToList(),
            signature.CreatedBy,
            signature.CreatedAt,
            signature.UpdatedBy,
            signature.UpdatedAt,
            signature.ClientId,
            signature.TenantId,
            signature.IsDeleted);
    }
}
