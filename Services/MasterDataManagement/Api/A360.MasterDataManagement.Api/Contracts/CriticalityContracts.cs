using A360.MasterDataManagement.Domain.Entities;
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
    string? Icon,
    string? Image,
    string? Likelihood,
    string? Impact,
    CriticalityBusinessImpactDto? BusinessImpact,
    string? RecommendedStrategy,
    string? InspectionFrequency,
    string? ConditionMonitoring,
    string? SparePartsPriority,
    int? ResponseTimeHours,
    bool? EscalateOnFailure,
    bool? RootCauseRequired,
    bool? ApprovalForDisposal,
    bool? ShowInReports,
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
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            Likelihood = string.IsNullOrWhiteSpace(Likelihood) ? null : Likelihood.Trim(),
            Impact = string.IsNullOrWhiteSpace(Impact) ? null : Impact.Trim(),
            BusinessImpact = BusinessImpact?.ToEntity() ?? new CriticalityBusinessImpact(),
            RecommendedStrategy = string.IsNullOrWhiteSpace(RecommendedStrategy) ? null : RecommendedStrategy.Trim(),
            InspectionFrequency = string.IsNullOrWhiteSpace(InspectionFrequency) ? null : InspectionFrequency.Trim(),
            ConditionMonitoring = string.IsNullOrWhiteSpace(ConditionMonitoring) ? null : ConditionMonitoring.Trim(),
            SparePartsPriority = string.IsNullOrWhiteSpace(SparePartsPriority) ? null : SparePartsPriority.Trim(),
            ResponseTimeHours = ResponseTimeHours,
            EscalateOnFailure = EscalateOnFailure ?? false,
            RootCauseRequired = RootCauseRequired ?? false,
            ApprovalForDisposal = ApprovalForDisposal ?? false,
            ShowInReports = ShowInReports ?? true,

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
    string? Icon,
    string? Image,
    string? Likelihood,
    string? Impact,
    CriticalityBusinessImpactDto? BusinessImpact,
    string? RecommendedStrategy,
    string? InspectionFrequency,
    string? ConditionMonitoring,
    string? SparePartsPriority,
    int? ResponseTimeHours,
    bool? EscalateOnFailure,
    bool? RootCauseRequired,
    bool? ApprovalForDisposal,
    bool? ShowInReports,
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
        criticality.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        criticality.Image = string.IsNullOrWhiteSpace(Image) ? null : Image;
        criticality.Likelihood = string.IsNullOrWhiteSpace(Likelihood) ? null : Likelihood.Trim();
        criticality.Impact = string.IsNullOrWhiteSpace(Impact) ? null : Impact.Trim();
        criticality.BusinessImpact = BusinessImpact?.ToEntity() ?? new CriticalityBusinessImpact();
        criticality.RecommendedStrategy = string.IsNullOrWhiteSpace(RecommendedStrategy) ? null : RecommendedStrategy.Trim();
        criticality.InspectionFrequency = string.IsNullOrWhiteSpace(InspectionFrequency) ? null : InspectionFrequency.Trim();
        criticality.ConditionMonitoring = string.IsNullOrWhiteSpace(ConditionMonitoring) ? null : ConditionMonitoring.Trim();
        criticality.SparePartsPriority = string.IsNullOrWhiteSpace(SparePartsPriority) ? null : SparePartsPriority.Trim();
        criticality.ResponseTimeHours = ResponseTimeHours;
        criticality.EscalateOnFailure = EscalateOnFailure ?? false;
        criticality.RootCauseRequired = RootCauseRequired ?? false;
        criticality.ApprovalForDisposal = ApprovalForDisposal ?? false;
        criticality.ShowInReports = ShowInReports ?? true;

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

public sealed record CriticalityBusinessImpactDto(
    string? Safety,
    string? Operational,
    string? Financial,
    string? Regulatory,
    string? Reputation,
    string? Environmental)
{
    public CriticalityBusinessImpact ToEntity()
    {
        return new CriticalityBusinessImpact
        {
            Safety = string.IsNullOrWhiteSpace(Safety) ? null : Safety.Trim(),
            Operational = string.IsNullOrWhiteSpace(Operational) ? null : Operational.Trim(),
            Financial = string.IsNullOrWhiteSpace(Financial) ? null : Financial.Trim(),
            Regulatory = string.IsNullOrWhiteSpace(Regulatory) ? null : Regulatory.Trim(),
            Reputation = string.IsNullOrWhiteSpace(Reputation) ? null : Reputation.Trim(),
            Environmental = string.IsNullOrWhiteSpace(Environmental) ? null : Environmental.Trim()
        };
    }

    public static CriticalityBusinessImpactDto FromEntity(CriticalityBusinessImpact? impact)
    {
        return new CriticalityBusinessImpactDto(
            impact?.Safety,
            impact?.Operational,
            impact?.Financial,
            impact?.Regulatory,
            impact?.Reputation,
            impact?.Environmental);
    }
}

public sealed record CriticalityResponse(
    string Id,
    string CriticalityName,
    string Code,
    string? RiskLevel,
    string? MaintenancePriority,
    string? Color,
    bool DefaultCriticality,
    string? Description,
    string? Icon,
    string? Image,
    string? Likelihood,
    string? Impact,
    CriticalityBusinessImpactDto BusinessImpact,
    string? RecommendedStrategy,
    string? InspectionFrequency,
    string? ConditionMonitoring,
    string? SparePartsPriority,
    int? ResponseTimeHours,
    bool EscalateOnFailure,
    bool RootCauseRequired,
    bool ApprovalForDisposal,
    bool ShowInReports,
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
            criticality.Icon,
            criticality.Image,
            criticality.Likelihood,
            criticality.Impact,
            CriticalityBusinessImpactDto.FromEntity(criticality.BusinessImpact),
            criticality.RecommendedStrategy,
            criticality.InspectionFrequency,
            criticality.ConditionMonitoring,
            criticality.SparePartsPriority,
            criticality.ResponseTimeHours,
            criticality.EscalateOnFailure,
            criticality.RootCauseRequired,
            criticality.ApprovalForDisposal,
            criticality.ShowInReports,
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
