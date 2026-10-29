using SiteEntity = A360.MasterDataManagement.Domain.Entities.Site;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateSiteRequest(
    string? SiteName,
    string? Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? Department,
    string? SiteType,
    string? City,
    string? Country,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SiteEntity ToEntity()
    {
        return new SiteEntity
        {
            SiteName = SiteName!.Trim(),
            Code = Code!.Trim(),
            BusinessUnit = BusinessUnit?.ToReference(),
            Department = Department?.ToReference(),
            SiteType = string.IsNullOrWhiteSpace(SiteType) ? null : SiteType.Trim(),
            City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSiteRequest(
    string? SiteName,
    string? Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? Department,
    string? SiteType,
    string? City,
    string? Country,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SiteEntity site)
    {
        site.SiteName = SiteName!.Trim();
        site.Code = Code!.Trim();
        site.BusinessUnit = BusinessUnit?.ToReference();
        site.Department = Department?.ToReference();
        site.SiteType = string.IsNullOrWhiteSpace(SiteType) ? null : SiteType.Trim();
        site.City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
        site.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();

        site.UpdatedBy = UpdatedBy;
        site.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            site.Status = Status;
        }
    }
}

public sealed record UpdateSiteStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record SiteResponse(
    string Id,
    string SiteName,
    string Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? Department,
    string? SiteType,
    string? City,
    string? Country,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SiteResponse FromEntity(SiteEntity site)
    {
        return new SiteResponse(
            site.Id ?? string.Empty,
            site.SiteName ?? string.Empty,
            site.Code ?? string.Empty,
            site.BusinessUnit is null
                ? null
                : MasterDataReferenceDto.FromReference(site.BusinessUnit),
            site.Department is null
                ? null
                : MasterDataReferenceDto.FromReference(site.Department),
            site.SiteType,
            site.City,
            site.Country,
            site.CreatedBy,
            site.CreatedAt,
            site.UpdatedBy,
            site.UpdatedAt,
            site.ClientId,
            site.TenantId,
            site.Status,
            site.IsDeleted
        );
    }
}
