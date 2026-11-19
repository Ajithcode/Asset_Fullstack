using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;
using A360.MasterDataManagement.Domain.Entities;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateOrganizationRequest(
    string? OrganizationName,
    string? Code,
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? BusinessUnits,
    List<MasterDataReferenceDto>? Sites,
    string? RegistrationNumber,
    string? Website,
    string? Description,
    OrganizationAddressDto? Address,
    OrganizationContactDto? Contact,
    OrganizationSettingsDto? Settings,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public OrganizationEntity ToEntity()
    {
        return new OrganizationEntity
        {
            OrganizationName = OrganizationName!.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            ParentOrganization = ParentOrganization?.ToReference(),
            BusinessUnits = BusinessUnits?.Select(x => x.ToReference()).ToList() ?? [],
            Sites = Sites?.Select(x => x.ToReference()).ToList() ?? [],
            Code = OrganizationText.Clean(Code),
            RegistrationNumber = OrganizationText.Clean(RegistrationNumber),
            Website = OrganizationText.Clean(Website),
            Description = OrganizationText.Clean(Description),
            Address = Address?.ToEntity(),
            Contact = Contact?.ToEntity(),
            Settings = Settings?.ToEntity(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateOrganizationRequest(
    string? OrganizationName,
    string? Code,
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? BusinessUnits,
    List<MasterDataReferenceDto>? Sites,
    string? RegistrationNumber,
    string? Website,
    string? Description,
    OrganizationAddressDto? Address,
    OrganizationContactDto? Contact,
    OrganizationSettingsDto? Settings,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(OrganizationEntity organization)
    {
        organization.OrganizationName = OrganizationName!.Trim();
        organization.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        organization.Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();
        organization.ParentOrganization = ParentOrganization?.ToReference();
        // The web form doesn't send these; keep the stored links unless a list is given
        if (BusinessUnits is not null) organization.BusinessUnits = BusinessUnits.Select(x => x.ToReference()).ToList();
        if (Sites is not null) organization.Sites = Sites.Select(x => x.ToReference()).ToList();
        organization.Code = OrganizationText.Clean(Code);
        organization.RegistrationNumber = OrganizationText.Clean(RegistrationNumber);
        organization.Website = OrganizationText.Clean(Website);
        organization.Description = OrganizationText.Clean(Description);
        organization.Address = Address?.ToEntity();
        organization.Contact = Contact?.ToEntity();
        organization.Settings = Settings?.ToEntity();

        organization.UpdatedBy = UpdatedBy;
        organization.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            organization.Status = Status;
        }
    }
}

public sealed record UpdateOrganizationStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record OrganizationResponse(
    string Id,
    string OrganizationName,
    string Code,
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    IReadOnlyList<MasterDataReferenceDto> BusinessUnits,
    IReadOnlyList<MasterDataReferenceDto> Sites,
    string? RegistrationNumber,
    string? Website,
    string? Description,
    OrganizationAddressDto? Address,
    OrganizationContactDto? Contact,
    OrganizationSettingsDto? Settings,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static OrganizationResponse FromEntity(OrganizationEntity organization)
    {
        return new OrganizationResponse(
            organization.Id ?? string.Empty,
            organization.OrganizationName ?? string.Empty,
            organization.Code ?? string.Empty,
            organization.LogoUrl,
            organization.Type,
            organization.ParentOrganization is null
                ? null
                : MasterDataReferenceDto.FromReference(organization.ParentOrganization),
            (organization.BusinessUnits ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (organization.Sites ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            organization.RegistrationNumber,
            organization.Website,
            organization.Description,
            OrganizationAddressDto.FromEntity(organization.Address),
            OrganizationContactDto.FromEntity(organization.Contact),
            OrganizationSettingsDto.FromEntity(organization.Settings),
            organization.CreatedBy,
            organization.CreatedAt,
            organization.UpdatedBy,
            organization.UpdatedAt,
            organization.ClientId,
            organization.TenantId,
            organization.Status,
            organization.IsDeleted
        );
    }
}

internal static class OrganizationText
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record OrganizationAddressDto(
    string? Line1, string? Line2, string? City, string? State, string? Country, string? PostalCode)
{
    public OrganizationAddress ToEntity() => new()
    {
        Line1 = OrganizationText.Clean(Line1),
        Line2 = OrganizationText.Clean(Line2),
        City = OrganizationText.Clean(City),
        State = OrganizationText.Clean(State),
        Country = OrganizationText.Clean(Country),
        PostalCode = OrganizationText.Clean(PostalCode)
    };

    public static OrganizationAddressDto? FromEntity(OrganizationAddress? a) =>
        a is null ? null : new(a.Line1, a.Line2, a.City, a.State, a.Country, a.PostalCode);
}

public sealed record OrganizationContactDto(
    string? Name, string? Designation, string? Email, string? PhoneCountry, string? Phone)
{
    public OrganizationContact ToEntity() => new()
    {
        Name = OrganizationText.Clean(Name),
        Designation = OrganizationText.Clean(Designation),
        Email = OrganizationText.Clean(Email),
        PhoneCountry = OrganizationText.Clean(PhoneCountry),
        Phone = OrganizationText.Clean(Phone)
    };

    public static OrganizationContactDto? FromEntity(OrganizationContact? c) =>
        c is null ? null : new(c.Name, c.Designation, c.Email, c.PhoneCountry, c.Phone);
}

public sealed record OrganizationSettingsDto(
    string? Currency, string? TimeZone, string? Language, string? DateFormat,
    string? EffectiveFrom, string? EffectiveTo,
    bool AllowSubOrganizations, bool AllowMultipleLocations, bool UseForAssetNumbering, bool EnableFinancialTracking)
{
    public OrganizationSettings ToEntity() => new()
    {
        Currency = OrganizationText.Clean(Currency),
        TimeZone = OrganizationText.Clean(TimeZone),
        Language = OrganizationText.Clean(Language),
        DateFormat = OrganizationText.Clean(DateFormat),
        EffectiveFrom = OrganizationText.Clean(EffectiveFrom),
        EffectiveTo = OrganizationText.Clean(EffectiveTo),
        AllowSubOrganizations = AllowSubOrganizations,
        AllowMultipleLocations = AllowMultipleLocations,
        UseForAssetNumbering = UseForAssetNumbering,
        EnableFinancialTracking = EnableFinancialTracking
    };

    public static OrganizationSettingsDto? FromEntity(OrganizationSettings? s) =>
        s is null ? null : new(s.Currency, s.TimeZone, s.Language, s.DateFormat, s.EffectiveFrom, s.EffectiveTo,
            s.AllowSubOrganizations, s.AllowMultipleLocations, s.UseForAssetNumbering, s.EnableFinancialTracking);
}
