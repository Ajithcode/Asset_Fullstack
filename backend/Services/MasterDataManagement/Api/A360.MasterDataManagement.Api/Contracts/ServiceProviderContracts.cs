using ServiceProviderEntity = A360.MasterDataManagement.Domain.Entities.ServiceProvider;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateServiceProviderRequest(
    string? ServiceProviderName,
    string? Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? Logo,
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    decimal? Rating,
    List<string>? Services,
    List<string>? Skills,
    List<string>? Coverage,
    int? Technicians,
    int? ResponseHours,
    string? LicenceNo,
    DateOnly? LicenceExpiry,
    string? InsurancePolicy,
    DateOnly? InsuranceExpiry,
    bool? IsoCertified,
    bool? HseApproved,
    bool? Preferred,
    bool? Approved,
    bool? PortalAccess,
    bool? EmergencyCallout,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ServiceProviderEntity ToEntity()
    {
        return new ServiceProviderEntity
        {
            ServiceProviderName = ServiceProviderName!.Trim(),
            Code = Code!.Trim(),
            Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
            PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim(),
            ServiceType = string.IsNullOrWhiteSpace(ServiceType) ? null : ServiceType.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Logo = StructureText.Clean(Logo),
            BrandColor = StructureText.Clean(BrandColor),
            Address = Address?.ToValue(),
            AlternateContact = StructureText.Clean(AlternateContact),
            AlternateEmail = StructureText.Clean(AlternateEmail),
            AlternatePhone = StructureText.Clean(AlternatePhone),
            Documents = VendorDocumentDto.ToValues(Documents),
            Rating = Rating ?? 0,
            Services = StructureText.CleanList(Services),
            Skills = StructureText.CleanList(Skills),
            Coverage = StructureText.CleanList(Coverage),
            Technicians = Technicians ?? 0,
            ResponseHours = ResponseHours ?? 24,
            LicenceNo = StructureText.Clean(LicenceNo),
            LicenceExpiry = LicenceExpiry,
            InsurancePolicy = StructureText.Clean(InsurancePolicy),
            InsuranceExpiry = InsuranceExpiry,
            IsoCertified = IsoCertified ?? false,
            HseApproved = HseApproved ?? false,
            Preferred = Preferred ?? false,
            Approved = Approved ?? false,
            PortalAccess = PortalAccess ?? false,
            EmergencyCallout = EmergencyCallout ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateServiceProviderRequest(
    string? ServiceProviderName,
    string? Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? Logo,
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    decimal? Rating,
    List<string>? Services,
    List<string>? Skills,
    List<string>? Coverage,
    int? Technicians,
    int? ResponseHours,
    string? LicenceNo,
    DateOnly? LicenceExpiry,
    string? InsurancePolicy,
    DateOnly? InsuranceExpiry,
    bool? IsoCertified,
    bool? HseApproved,
    bool? Preferred,
    bool? Approved,
    bool? PortalAccess,
    bool? EmergencyCallout,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ServiceProviderEntity serviceProvider)
    {
        serviceProvider.ServiceProviderName = ServiceProviderName!.Trim();
        serviceProvider.Code = Code!.Trim();
        serviceProvider.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();
        serviceProvider.PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim();
        serviceProvider.ServiceType = string.IsNullOrWhiteSpace(ServiceType) ? null : ServiceType.Trim();
        serviceProvider.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        serviceProvider.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        serviceProvider.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        serviceProvider.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        serviceProvider.Logo = StructureText.Clean(Logo);
        serviceProvider.BrandColor = StructureText.Clean(BrandColor);
        serviceProvider.Address = Address?.ToValue();
        serviceProvider.AlternateContact = StructureText.Clean(AlternateContact);
        serviceProvider.AlternateEmail = StructureText.Clean(AlternateEmail);
        serviceProvider.AlternatePhone = StructureText.Clean(AlternatePhone);
        serviceProvider.Documents = VendorDocumentDto.ToValues(Documents);
        serviceProvider.Rating = Rating ?? 0;
        serviceProvider.Services = StructureText.CleanList(Services);
        serviceProvider.Skills = StructureText.CleanList(Skills);
        serviceProvider.Coverage = StructureText.CleanList(Coverage);
        serviceProvider.Technicians = Technicians ?? 0;
        serviceProvider.ResponseHours = ResponseHours ?? 24;
        serviceProvider.LicenceNo = StructureText.Clean(LicenceNo);
        serviceProvider.LicenceExpiry = LicenceExpiry;
        serviceProvider.InsurancePolicy = StructureText.Clean(InsurancePolicy);
        serviceProvider.InsuranceExpiry = InsuranceExpiry;
        serviceProvider.IsoCertified = IsoCertified ?? false;
        serviceProvider.HseApproved = HseApproved ?? false;
        serviceProvider.Preferred = Preferred ?? false;
        serviceProvider.Approved = Approved ?? false;
        serviceProvider.PortalAccess = PortalAccess ?? false;
        serviceProvider.EmergencyCallout = EmergencyCallout ?? false;

        serviceProvider.UpdatedBy = UpdatedBy;
        serviceProvider.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            serviceProvider.Status = Status;
        }
    }
}

public sealed record UpdateServiceProviderStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ServiceProviderResponse(
    string Id,
    string ServiceProviderName,
    string Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? Logo,
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    IReadOnlyList<VendorDocumentDto> Documents,
    decimal Rating,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Coverage,
    int Technicians,
    int ResponseHours,
    string? LicenceNo,
    DateOnly? LicenceExpiry,
    string? InsurancePolicy,
    DateOnly? InsuranceExpiry,
    bool IsoCertified,
    bool HseApproved,
    bool Preferred,
    bool Approved,
    bool PortalAccess,
    bool EmergencyCallout,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ServiceProviderResponse FromEntity(ServiceProviderEntity serviceProvider)
    {
        return new ServiceProviderResponse(
            serviceProvider.Id ?? string.Empty,
            serviceProvider.ServiceProviderName ?? string.Empty,
            serviceProvider.Code ?? string.Empty,
            serviceProvider.Website,
            serviceProvider.PrimaryContact,
            serviceProvider.ServiceType,
            serviceProvider.Email,
            serviceProvider.Country,
            serviceProvider.Phone,
            serviceProvider.Description,
            serviceProvider.Logo,
            serviceProvider.BrandColor,
            StructureAddressDto.FromValue(serviceProvider.Address),
            serviceProvider.AlternateContact,
            serviceProvider.AlternateEmail,
            serviceProvider.AlternatePhone,
            VendorDocumentDto.FromValues(serviceProvider.Documents),
            serviceProvider.Rating,
            serviceProvider.Services ?? [],
            serviceProvider.Skills ?? [],
            serviceProvider.Coverage ?? [],
            serviceProvider.Technicians,
            serviceProvider.ResponseHours,
            serviceProvider.LicenceNo,
            serviceProvider.LicenceExpiry,
            serviceProvider.InsurancePolicy,
            serviceProvider.InsuranceExpiry,
            serviceProvider.IsoCertified,
            serviceProvider.HseApproved,
            serviceProvider.Preferred,
            serviceProvider.Approved,
            serviceProvider.PortalAccess,
            serviceProvider.EmergencyCallout,
            serviceProvider.CreatedBy,
            serviceProvider.CreatedAt,
            serviceProvider.UpdatedBy,
            serviceProvider.UpdatedAt,
            serviceProvider.ClientId,
            serviceProvider.TenantId,
            serviceProvider.Status,
            serviceProvider.IsDeleted
        );
    }
}
