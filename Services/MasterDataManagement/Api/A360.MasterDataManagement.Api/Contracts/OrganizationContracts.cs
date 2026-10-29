using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateOrganizationRequest(
    string? OrganizationName,
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? BusinessUnits,
    List<MasterDataReferenceDto>? Sites,
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
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? BusinessUnits,
    List<MasterDataReferenceDto>? Sites,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(OrganizationEntity organization)
    {
        organization.OrganizationName = OrganizationName!.Trim();
        organization.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        organization.Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();
        organization.ParentOrganization = ParentOrganization?.ToReference();
        organization.BusinessUnits = BusinessUnits?.Select(x => x.ToReference()).ToList() ?? [];
        organization.Sites = Sites?.Select(x => x.ToReference()).ToList() ?? [];

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
    string? LogoUrl,
    string? Type,
    MasterDataReferenceDto? ParentOrganization,
    IReadOnlyList<MasterDataReferenceDto> BusinessUnits,
    IReadOnlyList<MasterDataReferenceDto> Sites,
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
            organization.LogoUrl,
            organization.Type,
            organization.ParentOrganization is null
                ? null
                : MasterDataReferenceDto.FromReference(organization.ParentOrganization),
            (organization.BusinessUnits ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (organization.Sites ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
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
