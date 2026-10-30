using A360.MasterDataManagement.Domain.Entities;
using ManufacturerEntity = A360.MasterDataManagement.Domain.Entities.Manufacturer;

namespace A360.MasterDataManagement.Api.Contracts;

// Models are asset models picked by id. On update, leaving Models out keeps the current models,
// while an empty list removes them all from this manufacturer.
public sealed record CreateManufacturerRequest(
    string? ManufacturerName,
    string? Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    List<MasterDataReferenceDto>? Models,
    string? Logo,
    string? BrandColor,
    string? Designation,
    string? SecondaryContact,
    string? SecondaryEmail,
    string? SecondaryPhone,
    ManufacturerAddressDto? Address,
    string? SupportPortal,
    string? SupportEmail,
    string? SupportPhone,
    int? StandardWarrantyMonths,
    int? SlaResponseHours,
    string? DefaultCurrency,
    string? PaymentTerms,
    bool? Preferred,
    bool? WarrantyRegistration,
    bool? AutoCreateWarranty,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ManufacturerEntity ToEntity()
    {
        return new ManufacturerEntity
        {
            ManufacturerName = ManufacturerName!.Trim(),
            Code = Code!.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
            Logo = string.IsNullOrWhiteSpace(Logo) ? null : Logo,
            BrandColor = string.IsNullOrWhiteSpace(BrandColor) ? null : BrandColor.Trim(),
            Designation = string.IsNullOrWhiteSpace(Designation) ? null : Designation.Trim(),
            SecondaryContact = string.IsNullOrWhiteSpace(SecondaryContact) ? null : SecondaryContact.Trim(),
            SecondaryEmail = string.IsNullOrWhiteSpace(SecondaryEmail) ? null : SecondaryEmail.Trim(),
            SecondaryPhone = string.IsNullOrWhiteSpace(SecondaryPhone) ? null : SecondaryPhone.Trim(),
            Address = Address?.ToEntity() ?? new ManufacturerAddress(),
            SupportPortal = string.IsNullOrWhiteSpace(SupportPortal) ? null : SupportPortal.Trim(),
            SupportEmail = string.IsNullOrWhiteSpace(SupportEmail) ? null : SupportEmail.Trim(),
            SupportPhone = string.IsNullOrWhiteSpace(SupportPhone) ? null : SupportPhone.Trim(),
            StandardWarrantyMonths = StandardWarrantyMonths,
            SlaResponseHours = SlaResponseHours,
            DefaultCurrency = string.IsNullOrWhiteSpace(DefaultCurrency) ? null : DefaultCurrency.Trim(),
            PaymentTerms = string.IsNullOrWhiteSpace(PaymentTerms) ? null : PaymentTerms.Trim(),
            Preferred = Preferred ?? false,
            WarrantyRegistration = WarrantyRegistration ?? false,
            AutoCreateWarranty = AutoCreateWarranty ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateManufacturerRequest(
    string? ManufacturerName,
    string? Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    List<MasterDataReferenceDto>? Models,
    string? Logo,
    string? BrandColor,
    string? Designation,
    string? SecondaryContact,
    string? SecondaryEmail,
    string? SecondaryPhone,
    ManufacturerAddressDto? Address,
    string? SupportPortal,
    string? SupportEmail,
    string? SupportPhone,
    int? StandardWarrantyMonths,
    int? SlaResponseHours,
    string? DefaultCurrency,
    string? PaymentTerms,
    bool? Preferred,
    bool? WarrantyRegistration,
    bool? AutoCreateWarranty,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ManufacturerEntity manufacturer)
    {
        manufacturer.ManufacturerName = ManufacturerName!.Trim();
        manufacturer.Code = Code!.Trim();
        manufacturer.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        manufacturer.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();
        manufacturer.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        manufacturer.ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim();
        manufacturer.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        manufacturer.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
        manufacturer.Logo = string.IsNullOrWhiteSpace(Logo) ? null : Logo;
        manufacturer.BrandColor = string.IsNullOrWhiteSpace(BrandColor) ? null : BrandColor.Trim();
        manufacturer.Designation = string.IsNullOrWhiteSpace(Designation) ? null : Designation.Trim();
        manufacturer.SecondaryContact = string.IsNullOrWhiteSpace(SecondaryContact) ? null : SecondaryContact.Trim();
        manufacturer.SecondaryEmail = string.IsNullOrWhiteSpace(SecondaryEmail) ? null : SecondaryEmail.Trim();
        manufacturer.SecondaryPhone = string.IsNullOrWhiteSpace(SecondaryPhone) ? null : SecondaryPhone.Trim();
        manufacturer.Address = Address?.ToEntity() ?? new ManufacturerAddress();
        manufacturer.SupportPortal = string.IsNullOrWhiteSpace(SupportPortal) ? null : SupportPortal.Trim();
        manufacturer.SupportEmail = string.IsNullOrWhiteSpace(SupportEmail) ? null : SupportEmail.Trim();
        manufacturer.SupportPhone = string.IsNullOrWhiteSpace(SupportPhone) ? null : SupportPhone.Trim();
        manufacturer.StandardWarrantyMonths = StandardWarrantyMonths;
        manufacturer.SlaResponseHours = SlaResponseHours;
        manufacturer.DefaultCurrency = string.IsNullOrWhiteSpace(DefaultCurrency) ? null : DefaultCurrency.Trim();
        manufacturer.PaymentTerms = string.IsNullOrWhiteSpace(PaymentTerms) ? null : PaymentTerms.Trim();
        manufacturer.Preferred = Preferred ?? false;
        manufacturer.WarrantyRegistration = WarrantyRegistration ?? false;
        manufacturer.AutoCreateWarranty = AutoCreateWarranty ?? false;

        manufacturer.UpdatedBy = UpdatedBy;
        manufacturer.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            manufacturer.Status = Status;
        }
    }
}

public sealed record UpdateManufacturerStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ManufacturerAddressDto(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country)
{
    public ManufacturerAddress ToEntity()
    {
        return new ManufacturerAddress
        {
            Line1 = string.IsNullOrWhiteSpace(Line1) ? null : Line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(Line2) ? null : Line2.Trim(),
            City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
            State = string.IsNullOrWhiteSpace(State) ? null : State.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(PostalCode) ? null : PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim()
        };
    }

    public static ManufacturerAddressDto FromEntity(ManufacturerAddress? address)
    {
        return new ManufacturerAddressDto(
            address?.Line1,
            address?.Line2,
            address?.City,
            address?.State,
            address?.PostalCode,
            address?.Country);
    }
}

public sealed record ManufacturerResponse(
    string Id,
    string ManufacturerName,
    string Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<MasterDataReferenceDto> Models,
    int ModelCount,
    string? Logo,
    string? BrandColor,
    string? Designation,
    string? SecondaryContact,
    string? SecondaryEmail,
    string? SecondaryPhone,
    ManufacturerAddressDto Address,
    string? SupportPortal,
    string? SupportEmail,
    string? SupportPhone,
    int? StandardWarrantyMonths,
    int? SlaResponseHours,
    string? DefaultCurrency,
    string? PaymentTerms,
    bool Preferred,
    bool WarrantyRegistration,
    bool AutoCreateWarranty,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ManufacturerResponse FromEntity(
        ManufacturerEntity manufacturer,
        IReadOnlyList<MasterDataReferenceDto> models)
    {
        return new ManufacturerResponse(
            manufacturer.Id ?? string.Empty,
            manufacturer.ManufacturerName ?? string.Empty,
            manufacturer.Code ?? string.Empty,
            manufacturer.Country,
            manufacturer.Website,
            manufacturer.Description,
            manufacturer.ContactPerson,
            manufacturer.Email,
            manufacturer.PhoneNumber,
            models,
            models.Count,
            manufacturer.Logo,
            manufacturer.BrandColor,
            manufacturer.Designation,
            manufacturer.SecondaryContact,
            manufacturer.SecondaryEmail,
            manufacturer.SecondaryPhone,
            ManufacturerAddressDto.FromEntity(manufacturer.Address),
            manufacturer.SupportPortal,
            manufacturer.SupportEmail,
            manufacturer.SupportPhone,
            manufacturer.StandardWarrantyMonths,
            manufacturer.SlaResponseHours,
            manufacturer.DefaultCurrency,
            manufacturer.PaymentTerms,
            manufacturer.Preferred,
            manufacturer.WarrantyRegistration,
            manufacturer.AutoCreateWarranty,
            manufacturer.CreatedBy,
            manufacturer.CreatedAt,
            manufacturer.UpdatedBy,
            manufacturer.UpdatedAt,
            manufacturer.ClientId,
            manufacturer.TenantId,
            manufacturer.Status,
            manufacturer.IsDeleted
        );
    }
}
