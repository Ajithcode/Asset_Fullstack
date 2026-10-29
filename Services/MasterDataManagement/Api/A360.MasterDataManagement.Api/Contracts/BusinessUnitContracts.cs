using BusinessUnitEntity = A360.MasterDataManagement.Domain.Entities.BusinessUnit;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateBusinessUnitRequest(
    string? BusinessUnitName,
    string? LogoUrl,
    string? Code,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? Departments,
    List<MasterDataReferenceDto>? Sites,
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

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateBusinessUnitRequest(
    string? BusinessUnitName,
    string? LogoUrl,
    string? Code,
    MasterDataReferenceDto? ParentOrganization,
    List<MasterDataReferenceDto>? Departments,
    List<MasterDataReferenceDto>? Sites,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(BusinessUnitEntity businessUnit)
    {
        businessUnit.BusinessUnitName = BusinessUnitName!.Trim();
        businessUnit.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        businessUnit.Code = Code!.Trim();
        businessUnit.ParentOrganization = ParentOrganization?.ToReference();
        businessUnit.Departments = Departments?.Select(x => x.ToReference()).ToList() ?? [];
        businessUnit.Sites = Sites?.Select(x => x.ToReference()).ToList() ?? [];

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
