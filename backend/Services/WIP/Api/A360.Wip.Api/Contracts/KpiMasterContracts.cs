
using KpiMasterEntity = A360.Wip.Domain.Entities.KpiMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateKpiMasterRequest(
    string? KpiId,
    string? AssetId,
    string? AssetName,
    string? KpiName,
    string? FormulaDefinition,
    double? ThresholdGreen,
    double? ThresholdAmber,
    double? ThresholdRed,
    string? RefreshFrequency,
    string? WidgetType,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public KpiMasterEntity ToEntity()
    {
        return new KpiMasterEntity
        {
            KpiId = KpiId,
            AssetId = AssetId,
            AssetName = AssetName,

            KpiName = KpiName,
            FormulaDefinition = FormulaDefinition,

            ThresholdGreen = ThresholdGreen,
            ThresholdAmber = ThresholdAmber,
            ThresholdRed = ThresholdRed,

            RefreshFrequency = RefreshFrequency,
            WidgetType = WidgetType,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateKpiMasterRequest(
    string? AssetId,
    string? AssetName,
    string? KpiName,
    string? FormulaDefinition,
    double? ThresholdGreen,
    double? ThresholdAmber,
    double? ThresholdRed,
    string? RefreshFrequency,
    string? WidgetType,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(KpiMasterEntity kpiMaster)
    {
        kpiMaster.AssetId = AssetId;
        kpiMaster.AssetName = AssetName;

        kpiMaster.KpiName = KpiName;
        kpiMaster.FormulaDefinition = FormulaDefinition;

        kpiMaster.ThresholdGreen = ThresholdGreen;
        kpiMaster.ThresholdAmber = ThresholdAmber;
        kpiMaster.ThresholdRed = ThresholdRed;

        kpiMaster.RefreshFrequency = RefreshFrequency;
        kpiMaster.WidgetType = WidgetType;

        kpiMaster.UpdatedBy = UpdatedBy;
        kpiMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            kpiMaster.Status = Status;
        }
    }
}

public sealed record KpiMasterResponse(
    string Id,
    string KpiId,
    string? AssetId,
    string? AssetName,
    string KpiName,
    string? FormulaDefinition,
    double? ThresholdGreen,
    double? ThresholdAmber,
    double? ThresholdRed,
    string? RefreshFrequency,
    string? WidgetType,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static KpiMasterResponse FromEntity(KpiMasterEntity kpiMaster)
    {
        return new KpiMasterResponse(
            kpiMaster.Id ?? string.Empty,
            kpiMaster.KpiId ?? string.Empty,
            kpiMaster.AssetId,
            kpiMaster.AssetName,

            kpiMaster.KpiName ?? string.Empty,
            kpiMaster.FormulaDefinition,

            kpiMaster.ThresholdGreen,
            kpiMaster.ThresholdAmber,
            kpiMaster.ThresholdRed,

            kpiMaster.RefreshFrequency,
            kpiMaster.WidgetType,

            kpiMaster.CreatedBy,
            kpiMaster.CreatedAt,
            kpiMaster.UpdatedBy,
            kpiMaster.UpdatedAt,

            kpiMaster.ClientId,
            kpiMaster.TenantId,
            kpiMaster.Status,
            kpiMaster.IsDeleted
        );
    }
}
