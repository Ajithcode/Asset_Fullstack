using AmcVendorEntity = A360.MasterDataManagement.Domain.Entities.AmcVendor;

namespace A360.MasterDataManagement.Api.Contracts;

// Dates are sent and returned as "yyyy-MM-dd".
public sealed record CreateAmcVendorRequest(
    string? AmcVendorName,
    string? Code,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? Logo,
    string? BrandColor,
    string? Website,
    string? Phone,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    string? ContractNo,
    decimal? ContractValue,
    string? Currency,
    string? BillingCycle,
    int? RenewalNoticeDays,
    bool? AutoRenew,
    List<string>? CoveredModels,
    int? VisitsPerYear,
    int? ResponseHours,
    int? ResolutionHours,
    decimal? UptimePercent,
    decimal? PenaltyPerHour,
    bool? NotifyBeforeExpiry,
    bool? ServiceReportRequired,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AmcVendorEntity ToEntity()
    {
        return new AmcVendorEntity
        {
            AmcVendorName = AmcVendorName!.Trim(),
            Code = Code!.Trim(),
            StartDate = StartDate!.Value,
            EndDate = EndDate!.Value,
            PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim(),
            AssetType = AssetType?.ToReference(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            ContractType = string.IsNullOrWhiteSpace(ContractType) ? null : ContractType.Trim(),
            Logo = StructureText.Clean(Logo),
            BrandColor = StructureText.Clean(BrandColor),
            Website = StructureText.Clean(Website),
            Phone = StructureText.Clean(Phone),
            Address = Address?.ToValue(),
            AlternateContact = StructureText.Clean(AlternateContact),
            AlternateEmail = StructureText.Clean(AlternateEmail),
            AlternatePhone = StructureText.Clean(AlternatePhone),
            Documents = VendorDocumentDto.ToValues(Documents),
            ContractNo = StructureText.Clean(ContractNo),
            ContractValue = ContractValue,
            Currency = StructureText.Clean(Currency),
            BillingCycle = StructureText.Clean(BillingCycle),
            RenewalNoticeDays = RenewalNoticeDays ?? 0,
            AutoRenew = AutoRenew ?? false,
            CoveredModels = StructureText.CleanList(CoveredModels),
            VisitsPerYear = VisitsPerYear ?? 0,
            ResponseHours = ResponseHours ?? 0,
            ResolutionHours = ResolutionHours ?? 0,
            UptimePercent = UptimePercent ?? 0,
            PenaltyPerHour = PenaltyPerHour,
            NotifyBeforeExpiry = NotifyBeforeExpiry ?? false,
            ServiceReportRequired = ServiceReportRequired ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAmcVendorRequest(
    string? AmcVendorName,
    string? Code,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? Logo,
    string? BrandColor,
    string? Website,
    string? Phone,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    string? ContractNo,
    decimal? ContractValue,
    string? Currency,
    string? BillingCycle,
    int? RenewalNoticeDays,
    bool? AutoRenew,
    List<string>? CoveredModels,
    int? VisitsPerYear,
    int? ResponseHours,
    int? ResolutionHours,
    decimal? UptimePercent,
    decimal? PenaltyPerHour,
    bool? NotifyBeforeExpiry,
    bool? ServiceReportRequired,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AmcVendorEntity amcVendor)
    {
        amcVendor.AmcVendorName = AmcVendorName!.Trim();
        amcVendor.Code = Code!.Trim();
        amcVendor.StartDate = StartDate!.Value;
        amcVendor.EndDate = EndDate!.Value;
        amcVendor.PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim();
        amcVendor.AssetType = AssetType?.ToReference();
        amcVendor.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        amcVendor.ContractType = string.IsNullOrWhiteSpace(ContractType) ? null : ContractType.Trim();
        amcVendor.Logo = StructureText.Clean(Logo);
        amcVendor.BrandColor = StructureText.Clean(BrandColor);
        amcVendor.Website = StructureText.Clean(Website);
        amcVendor.Phone = StructureText.Clean(Phone);
        amcVendor.Address = Address?.ToValue();
        amcVendor.AlternateContact = StructureText.Clean(AlternateContact);
        amcVendor.AlternateEmail = StructureText.Clean(AlternateEmail);
        amcVendor.AlternatePhone = StructureText.Clean(AlternatePhone);
        amcVendor.Documents = VendorDocumentDto.ToValues(Documents);
        amcVendor.ContractNo = StructureText.Clean(ContractNo);
        amcVendor.ContractValue = ContractValue;
        amcVendor.Currency = StructureText.Clean(Currency);
        amcVendor.BillingCycle = StructureText.Clean(BillingCycle);
        amcVendor.RenewalNoticeDays = RenewalNoticeDays ?? 0;
        amcVendor.AutoRenew = AutoRenew ?? false;
        amcVendor.CoveredModels = StructureText.CleanList(CoveredModels);
        amcVendor.VisitsPerYear = VisitsPerYear ?? 0;
        amcVendor.ResponseHours = ResponseHours ?? 0;
        amcVendor.ResolutionHours = ResolutionHours ?? 0;
        amcVendor.UptimePercent = UptimePercent ?? 0;
        amcVendor.PenaltyPerHour = PenaltyPerHour;
        amcVendor.NotifyBeforeExpiry = NotifyBeforeExpiry ?? false;
        amcVendor.ServiceReportRequired = ServiceReportRequired ?? false;

        amcVendor.UpdatedBy = UpdatedBy;
        amcVendor.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            amcVendor.Status = Status;
        }
    }
}

public sealed record UpdateAmcVendorStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AmcVendorResponse(
    string Id,
    string AmcVendorName,
    string Code,
    DateOnly StartDate,
    DateOnly EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? Logo,
    string? BrandColor,
    string? Website,
    string? Phone,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    IReadOnlyList<VendorDocumentDto> Documents,
    string? ContractNo,
    decimal? ContractValue,
    string? Currency,
    string? BillingCycle,
    int RenewalNoticeDays,
    bool AutoRenew,
    IReadOnlyList<string> CoveredModels,
    int VisitsPerYear,
    int ResponseHours,
    int ResolutionHours,
    decimal UptimePercent,
    decimal? PenaltyPerHour,
    bool NotifyBeforeExpiry,
    bool ServiceReportRequired,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AmcVendorResponse FromEntity(AmcVendorEntity amcVendor)
    {
        return new AmcVendorResponse(
            amcVendor.Id ?? string.Empty,
            amcVendor.AmcVendorName ?? string.Empty,
            amcVendor.Code ?? string.Empty,
            amcVendor.StartDate,
            amcVendor.EndDate,
            amcVendor.PrimaryContact,
            amcVendor.AssetType is null
                ? null
                : MasterDataReferenceDto.FromReference(amcVendor.AssetType),
            amcVendor.Email,
            amcVendor.ContractType,
            amcVendor.Logo,
            amcVendor.BrandColor,
            amcVendor.Website,
            amcVendor.Phone,
            StructureAddressDto.FromValue(amcVendor.Address),
            amcVendor.AlternateContact,
            amcVendor.AlternateEmail,
            amcVendor.AlternatePhone,
            VendorDocumentDto.FromValues(amcVendor.Documents),
            amcVendor.ContractNo,
            amcVendor.ContractValue,
            amcVendor.Currency,
            amcVendor.BillingCycle,
            amcVendor.RenewalNoticeDays,
            amcVendor.AutoRenew,
            amcVendor.CoveredModels ?? [],
            amcVendor.VisitsPerYear,
            amcVendor.ResponseHours,
            amcVendor.ResolutionHours,
            amcVendor.UptimePercent,
            amcVendor.PenaltyPerHour,
            amcVendor.NotifyBeforeExpiry,
            amcVendor.ServiceReportRequired,
            amcVendor.CreatedBy,
            amcVendor.CreatedAt,
            amcVendor.UpdatedBy,
            amcVendor.UpdatedAt,
            amcVendor.ClientId,
            amcVendor.TenantId,
            amcVendor.Status,
            amcVendor.IsDeleted
        );
    }
}
