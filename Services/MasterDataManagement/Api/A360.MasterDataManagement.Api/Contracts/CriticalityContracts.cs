using CriticalityEntity = A360.MasterDataManagement.Domain.Entities.Criticality;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateCriticalityRequest(
    string? CriticalityName,
    string? Code,
    string? RiskLevel,
    string? MaintenancePriority,
    string? Color,
    bool? DefaultCriticality,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public CriticalityEntity ToEntity()
    {
        return new CriticalityEntity
        {
            CriticalityName = CriticalityName!.Trim(),
            Code = Code!.Trim(),
            RiskLevel = string.IsNullOrWhiteSpace(RiskLevel) ? null : RiskLevel.Trim(),
            MaintenancePriority = string.IsNullOrWhiteSpace(MaintenancePriority) ? null : MaintenancePriority.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            DefaultCriticality = DefaultCriticality ?? false,
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

public sealed record UpdateCriticalityRequest(
    string? CriticalityName,
    string? Code,
    string? RiskLevel,
    string? MaintenancePriority,
    string? Color,
    bool? DefaultCriticality,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(CriticalityEntity criticality)
    {
        criticality.CriticalityName = CriticalityName!.Trim();
        criticality.Code = Code!.Trim();
        criticality.RiskLevel = string.IsNullOrWhiteSpace(RiskLevel) ? null : RiskLevel.Trim();
        criticality.MaintenancePriority = string.IsNullOrWhiteSpace(MaintenancePriority) ? null : MaintenancePriority.Trim();
        criticality.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        criticality.DefaultCriticality = DefaultCriticality ?? false;
        criticality.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        criticality.UpdatedBy = UpdatedBy;
        criticality.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            criticality.Status = Status;
        }
    }
}

public sealed record UpdateCriticalityStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record CriticalityResponse(
    string Id,
    string CriticalityName,
    string Code,
    string? RiskLevel,
    string? MaintenancePriority,
    string? Color,
    bool DefaultCriticality,
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
    public static CriticalityResponse FromEntity(CriticalityEntity criticality)
    {
        return new CriticalityResponse(
            criticality.Id ?? string.Empty,
            criticality.CriticalityName ?? string.Empty,
            criticality.Code ?? string.Empty,
            criticality.RiskLevel,
            criticality.MaintenancePriority,
            criticality.Color,
            criticality.DefaultCriticality,
            criticality.Description,
            criticality.CreatedBy,
            criticality.CreatedAt,
            criticality.UpdatedBy,
            criticality.UpdatedAt,
            criticality.ClientId,
            criticality.TenantId,
            criticality.Status,
            criticality.IsDeleted
        );
    }
}
