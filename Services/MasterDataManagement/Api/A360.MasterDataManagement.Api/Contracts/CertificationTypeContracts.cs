using CertificationTypeEntity = A360.MasterDataManagement.Domain.Entities.CertificationType;

namespace A360.MasterDataManagement.Api.Contracts;

public static class CertificationApplicability
{
    public static readonly string[] All = ["Asset", "Vendor", "People"];

    /// <summary>Returns the matching value in its standard spelling, or null when it is not one of <see cref="All"/>.</summary>
    public static string? Normalize(string? value)
    {
        return All.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Keeps the known values, in standard spelling and <see cref="All"/> order, without repeats.</summary>
    public static List<string> NormalizeAll(IEnumerable<string>? values)
    {
        var given = (values ?? []).Select(Normalize).ToHashSet();

        return All.Where(given.Contains).ToList();
    }
}

public sealed record CreateCertificationTypeRequest(
    string? CertificationTypeName,
    string? Code,
    string? Category,
    List<string>? ApplicableTo,
    int? ValidityMonths,
    int? RenewalReminderDays,
    string? Description,
    string? IssuingBody,
    string? StandardCode,
    List<string>? RequiredDocuments,
    bool? InspectionRequired,
    bool? BlockWhenExpired,
    bool? NotifyOwner,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public CertificationTypeEntity ToEntity()
    {
        return new CertificationTypeEntity
        {
            CertificationTypeName = CertificationTypeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            ApplicableTo = CertificationApplicability.NormalizeAll(ApplicableTo),
            ValidityMonths = ValidityMonths,
            RenewalReminderDays = RenewalReminderDays,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            IssuingBody = string.IsNullOrWhiteSpace(IssuingBody) ? null : IssuingBody.Trim(),
            StandardCode = string.IsNullOrWhiteSpace(StandardCode) ? null : StandardCode.Trim(),
            RequiredDocuments = RequiredDocuments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            InspectionRequired = InspectionRequired ?? false,
            BlockWhenExpired = BlockWhenExpired ?? false,
            NotifyOwner = NotifyOwner ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateCertificationTypeRequest(
    string? CertificationTypeName,
    string? Code,
    string? Category,
    List<string>? ApplicableTo,
    int? ValidityMonths,
    int? RenewalReminderDays,
    string? Description,
    string? IssuingBody,
    string? StandardCode,
    List<string>? RequiredDocuments,
    bool? InspectionRequired,
    bool? BlockWhenExpired,
    bool? NotifyOwner,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(CertificationTypeEntity certificationType)
    {
        certificationType.CertificationTypeName = CertificationTypeName!.Trim();
        certificationType.Code = Code!.Trim();
        certificationType.Category = Category!.Trim();
        certificationType.ApplicableTo = CertificationApplicability.NormalizeAll(ApplicableTo);
        certificationType.ValidityMonths = ValidityMonths;
        certificationType.RenewalReminderDays = RenewalReminderDays;
        certificationType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        certificationType.IssuingBody = string.IsNullOrWhiteSpace(IssuingBody) ? null : IssuingBody.Trim();
        certificationType.StandardCode = string.IsNullOrWhiteSpace(StandardCode) ? null : StandardCode.Trim();
        certificationType.RequiredDocuments = RequiredDocuments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        certificationType.InspectionRequired = InspectionRequired ?? false;
        certificationType.BlockWhenExpired = BlockWhenExpired ?? false;
        certificationType.NotifyOwner = NotifyOwner ?? false;

        certificationType.UpdatedBy = UpdatedBy;
        certificationType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            certificationType.Status = Status;
        }
    }
}

public sealed record UpdateCertificationTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record CertificationTypeResponse(
    string Id,
    string CertificationTypeName,
    string Code,
    string Category,
    IReadOnlyList<string> ApplicableTo,
    int? ValidityMonths,
    int? RenewalReminderDays,
    string? Description,
    string? IssuingBody,
    string? StandardCode,
    IReadOnlyList<string> RequiredDocuments,
    bool InspectionRequired,
    bool BlockWhenExpired,
    bool NotifyOwner,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static CertificationTypeResponse FromEntity(CertificationTypeEntity certificationType)
    {
        return new CertificationTypeResponse(
            certificationType.Id ?? string.Empty,
            certificationType.CertificationTypeName ?? string.Empty,
            certificationType.Code ?? string.Empty,
            certificationType.Category ?? string.Empty,
            certificationType.ApplicableTo ?? [],
            certificationType.ValidityMonths,
            certificationType.RenewalReminderDays,
            certificationType.Description,
            certificationType.IssuingBody,
            certificationType.StandardCode,
            certificationType.RequiredDocuments ?? [],
            certificationType.InspectionRequired,
            certificationType.BlockWhenExpired,
            certificationType.NotifyOwner,
            certificationType.CreatedBy,
            certificationType.CreatedAt,
            certificationType.UpdatedBy,
            certificationType.UpdatedAt,
            certificationType.ClientId,
            certificationType.TenantId,
            certificationType.Status,
            certificationType.IsDeleted
        );
    }
}
