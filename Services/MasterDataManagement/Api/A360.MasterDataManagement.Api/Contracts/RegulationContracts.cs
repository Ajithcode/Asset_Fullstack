using RegulationEntity = A360.MasterDataManagement.Domain.Entities.Regulation;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateRegulationRequest(
    string? RegulationName,
    string? Code,
    string? Country,
    string? Category,
    string? IssuingAuthority,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public RegulationEntity ToEntity()
    {
        return new RegulationEntity
        {
            RegulationName = RegulationName!.Trim(),
            Code = Code!.Trim(),
            Country = Country!.Trim(),
            Category = Category!.Trim(),
            IssuingAuthority = string.IsNullOrWhiteSpace(IssuingAuthority) ? null : IssuingAuthority.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateRegulationRequest(
    string? RegulationName,
    string? Code,
    string? Country,
    string? Category,
    string? IssuingAuthority,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(RegulationEntity regulation)
    {
        regulation.RegulationName = RegulationName!.Trim();
        regulation.Code = Code!.Trim();
        regulation.Country = Country!.Trim();
        regulation.Category = Category!.Trim();
        regulation.IssuingAuthority = string.IsNullOrWhiteSpace(IssuingAuthority) ? null : IssuingAuthority.Trim();
        regulation.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        regulation.UpdatedBy = UpdatedBy;
        regulation.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            regulation.Status = Status;
        }
    }
}

public sealed record UpdateRegulationStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record RegulationResponse(
    string Id,
    string RegulationName,
    string Code,
    string Country,
    string Category,
    string? IssuingAuthority,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static RegulationResponse FromEntity(RegulationEntity regulation)
    {
        return new RegulationResponse(
            regulation.Id ?? string.Empty,
            regulation.RegulationName ?? string.Empty,
            regulation.Code ?? string.Empty,
            regulation.Country ?? string.Empty,
            regulation.Category ?? string.Empty,
            regulation.IssuingAuthority,
            regulation.Description,
            regulation.CreatedBy,
            regulation.CreatedAt,
            regulation.UpdatedBy,
            regulation.UpdatedAt,
            regulation.ClientId,
            regulation.TenantId,
            regulation.Status,
            regulation.IsDeleted
        );
    }
}
