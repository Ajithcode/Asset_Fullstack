using BusinessUnitEntity = A360.MasterDataManagement.Domain.Entities.BusinessUnit;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateBusinessUnitRequest(
    string? BusinessUnitName,
    string? LogoUrl,
    string? Code,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? Departments,
    List<MasterDataReferenceDto>? Sites,
    MasterDataReferenceDto? ParentBusinessUnit,
    string? Icon,
    string? Description,
    string? Head,
    string? CostCenter,
    string? Currency,
    StructureAddressDto? Address,
    string? ContactEmail,
    string? PhoneCountry,
    string? Phone,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool AllowSites,
    bool BudgetTracking,
    bool AssetNumbering,
    bool Approvals,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public BusinessUnitEntity ToEntity()
    {
        return new BusinessUnitEntity
        {
            BusinessUnitName = BusinessUnitName!.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
            Code = Code!.Trim(),
            ParentOrganization = ParentOrganization?.ToReference(),
            Departments = Departments?.Select(x => x.ToReference()).ToList() ?? [],
            Sites = Sites?.Select(x => x.ToReference()).ToList() ?? [],

            ParentBusinessUnit = ParentBusinessUnit?.ToReference(),
            Icon = StructureText.Clean(Icon),
            Description = StructureText.Clean(Description),
            Head = StructureText.Clean(Head),
            CostCenter = StructureText.Clean(CostCenter),
            Currency = StructureText.Clean(Currency),
            Address = Address?.ToValue(),
            ContactEmail = StructureText.Clean(ContactEmail),
            PhoneCountry = StructureText.Clean(PhoneCountry),
            Phone = StructureText.Clean(Phone),
            EffectiveFrom = StructureText.Clean(EffectiveFrom),
            EffectiveTo = StructureText.Clean(EffectiveTo),
            AllowSites = AllowSites,
            BudgetTracking = BudgetTracking,
            AssetNumbering = AssetNumbering,
            Approvals = Approvals,
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

// Leaving Departments or Sites out keeps the current lists; an empty list clears them.
public sealed record UpdateBusinessUnitRequest(
    string? BusinessUnitName,
    string? LogoUrl,
    string? Code,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? Departments,
    List<MasterDataReferenceDto>? Sites,
    MasterDataReferenceDto? ParentBusinessUnit,
    string? Icon,
    string? Description,
    string? Head,
    string? CostCenter,
    string? Currency,
    StructureAddressDto? Address,
    string? ContactEmail,
    string? PhoneCountry,
    string? Phone,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool AllowSites,
    bool BudgetTracking,
    bool AssetNumbering,
    bool Approvals,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(BusinessUnitEntity businessUnit)
    {
        businessUnit.BusinessUnitName = BusinessUnitName!.Trim();
        businessUnit.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        businessUnit.Code = Code!.Trim();
        businessUnit.ParentOrganization = ParentOrganization?.ToReference();
        if (Departments is not null)
        {
            businessUnit.Departments = Departments.Select(x => x.ToReference()).ToList();
        }

        if (Sites is not null)
        {
            businessUnit.Sites = Sites.Select(x => x.ToReference()).ToList();
        }

        businessUnit.ParentBusinessUnit = ParentBusinessUnit?.ToReference();
        businessUnit.Icon = StructureText.Clean(Icon);
        businessUnit.Description = StructureText.Clean(Description);
        businessUnit.Head = StructureText.Clean(Head);
        businessUnit.CostCenter = StructureText.Clean(CostCenter);
        businessUnit.Currency = StructureText.Clean(Currency);
        businessUnit.Address = Address?.ToValue();
        businessUnit.ContactEmail = StructureText.Clean(ContactEmail);
        businessUnit.PhoneCountry = StructureText.Clean(PhoneCountry);
        businessUnit.Phone = StructureText.Clean(Phone);
        businessUnit.EffectiveFrom = StructureText.Clean(EffectiveFrom);
        businessUnit.EffectiveTo = StructureText.Clean(EffectiveTo);
        businessUnit.AllowSites = AllowSites;
        businessUnit.BudgetTracking = BudgetTracking;
        businessUnit.AssetNumbering = AssetNumbering;
        businessUnit.Approvals = Approvals;
        businessUnit.UpdatedBy = UpdatedBy;
        businessUnit.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            businessUnit.Status = Status;
        }
    }
}

public sealed record UpdateBusinessUnitStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record BusinessUnitResponse(
    string Id,
    string BusinessUnitName,
    string? LogoUrl,
    string Code,
    MasterDataReferenceDto? ParentOrganization,
    IReadOnlyList<MasterDataReferenceDto> Departments,
    IReadOnlyList<MasterDataReferenceDto> Sites,
    MasterDataReferenceDto? ParentBusinessUnit,
    string? Icon,
    string? Description,
    string? Head,
    string? CostCenter,
    string? Currency,
    StructureAddressDto? Address,
    string? ContactEmail,
    string? PhoneCountry,
    string? Phone,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool AllowSites,
    bool BudgetTracking,
    bool AssetNumbering,
    bool Approvals,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static BusinessUnitResponse FromEntity(BusinessUnitEntity businessUnit)
    {
        return new BusinessUnitResponse(
            businessUnit.Id ?? string.Empty,
            businessUnit.BusinessUnitName ?? string.Empty,
            businessUnit.LogoUrl,
            businessUnit.Code ?? string.Empty,
            businessUnit.ParentOrganization is null
                ? null
                : MasterDataReferenceDto.FromReference(businessUnit.ParentOrganization),
            (businessUnit.Departments ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (businessUnit.Sites ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            businessUnit.ParentBusinessUnit is null ? null : MasterDataReferenceDto.FromReference(businessUnit.ParentBusinessUnit),
            businessUnit.Icon,
            businessUnit.Description,
            businessUnit.Head,
            businessUnit.CostCenter,
            businessUnit.Currency,
            StructureAddressDto.FromValue(businessUnit.Address),
            businessUnit.ContactEmail,
            businessUnit.PhoneCountry,
            businessUnit.Phone,
            businessUnit.EffectiveFrom,
            businessUnit.EffectiveTo,
            businessUnit.AllowSites,
            businessUnit.BudgetTracking,
            businessUnit.AssetNumbering,
            businessUnit.Approvals,
            businessUnit.CreatedBy,
            businessUnit.CreatedAt,
            businessUnit.UpdatedBy,
            businessUnit.UpdatedAt,
            businessUnit.ClientId,
            businessUnit.TenantId,
            businessUnit.Status,
            businessUnit.IsDeleted
        );
    }
}
