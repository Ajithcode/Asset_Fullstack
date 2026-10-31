using A360.MasterDataManagement.Domain.ValueObjects;
using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record OrganizationSettingsDto(
    string? Currency,
    string? TimeZone,
    string? Language,
    string? DateFormat,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool AllowSubOrganizations,
    bool AllowMultipleLocations,
    bool UseForAssetNumbering,
    bool EnableFinancialTracking)
{
    public OrganizationSettings ToValue()
    {
        return new OrganizationSettings
        {
            Currency = StructureText.Clean(Currency),
            TimeZone = StructureText.Clean(TimeZone),
            Language = StructureText.Clean(Language),
            DateFormat = StructureText.Clean(DateFormat),
            EffectiveFrom = StructureText.Clean(EffectiveFrom),
            EffectiveTo = StructureText.Clean(EffectiveTo),
            AllowSubOrganizations = AllowSubOrganizations,
            AllowMultipleLocations = AllowMultipleLocations,
            UseForAssetNumbering = UseForAssetNumbering,
            EnableFinancialTracking = EnableFinancialTracking
        };
    }

    public static OrganizationSettingsDto? FromValue(OrganizationSettings? settings)
    {
        return settings is null
            ? null
            : new OrganizationSettingsDto(
                settings.Currency,
                settings.TimeZone,
                settings.Language,
                settings.DateFormat,
                settings.EffectiveFrom,
                settings.EffectiveTo,
                settings.AllowSubOrganizations,
                settings.AllowMultipleLocations,
                settings.UseForAssetNumbering,
                settings.EnableFinancialTracking);
    }
}

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
    StructureAddressDto? Address,
    StructureContactDto? Contact,
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
            Code = Code!.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            ParentOrganization = ParentOrganization?.ToReference(),
            BusinessUnits = BusinessUnits?.Select(x => x.ToReference()).ToList() ?? [],
            Sites = Sites?.Select(x => x.ToReference()).ToList() ?? [],
            RegistrationNumber = StructureText.Clean(RegistrationNumber),
            Website = StructureText.Clean(Website),
            Description = StructureText.Clean(Description),
            Address = Address?.ToValue(),
            Contact = Contact?.ToValue(),
            Settings = Settings?.ToValue(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

// Leaving BusinessUnits or Sites out keeps the current lists; an empty list clears them.
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
    StructureAddressDto? Address,
    StructureContactDto? Contact,
    OrganizationSettingsDto? Settings,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(OrganizationEntity organization)
    {
        organization.OrganizationName = OrganizationName!.Trim();
        organization.Code = Code!.Trim();
        organization.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        organization.Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();
        organization.ParentOrganization = ParentOrganization?.ToReference();

        if (BusinessUnits is not null)
        {
            organization.BusinessUnits = BusinessUnits.Select(x => x.ToReference()).ToList();
        }

        if (Sites is not null)
        {
            organization.Sites = Sites.Select(x => x.ToReference()).ToList();
        }

        organization.RegistrationNumber = StructureText.Clean(RegistrationNumber);
        organization.Website = StructureText.Clean(Website);
        organization.Description = StructureText.Clean(Description);
        organization.Address = Address?.ToValue();
        organization.Contact = Contact?.ToValue();
        organization.Settings = Settings?.ToValue();

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
    StructureAddressDto? Address,
    StructureContactDto? Contact,
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
            StructureAddressDto.FromValue(organization.Address),
            StructureContactDto.FromValue(organization.Contact),
            OrganizationSettingsDto.FromValue(organization.Settings),
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
