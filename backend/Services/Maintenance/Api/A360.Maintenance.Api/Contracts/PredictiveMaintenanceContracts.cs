
using PredictiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PredictiveMaintenance;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreatePredictiveMaintenanceRequest(
    string? AssetId,
    string? AssetName,
    string? SensorType,
    double ThresholdValue,
    string? AlertCondition,
    string? DataSourceDeviceId,
    string? PredictionModelOutput,
    string? RiskLevel,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public PredictiveMaintenanceEntity ToEntity()
    {
        return new PredictiveMaintenanceEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,
            SensorType = SensorType,
            ThresholdValue = ThresholdValue,
            AlertCondition = AlertCondition,
            DataSourceDeviceId = DataSourceDeviceId,
            PredictionModelOutput = PredictionModelOutput,
            RiskLevel = RiskLevel,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePredictiveMaintenanceRequest(
    string? AssetId,
    string? AssetName,
    string? SensorType,
    double ThresholdValue,
    string? AlertCondition,
    string? DataSourceDeviceId,
    string? PredictionModelOutput,
    string? RiskLevel,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(PredictiveMaintenanceEntity predictiveMaintenance)
    {
        predictiveMaintenance.AssetId = AssetId;
        predictiveMaintenance.AssetName = AssetName;
        predictiveMaintenance.SensorType = SensorType;
        predictiveMaintenance.ThresholdValue = ThresholdValue;
        predictiveMaintenance.AlertCondition = AlertCondition;
        predictiveMaintenance.DataSourceDeviceId = DataSourceDeviceId;
        predictiveMaintenance.PredictionModelOutput = PredictionModelOutput;
        predictiveMaintenance.RiskLevel = RiskLevel;

        predictiveMaintenance.UpdatedBy = UpdatedBy;
        predictiveMaintenance.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            predictiveMaintenance.Status = Status;
        }
    }
}

public sealed record PredictiveMaintenanceResponse(
    string Id,
    string AssetId,
    string AssetName,
    string SensorType,
    double ThresholdValue,
    string? AlertCondition,
    string? DataSourceDeviceId,
    string? PredictionModelOutput,
    string RiskLevel,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PredictiveMaintenanceResponse FromEntity(PredictiveMaintenanceEntity predictiveMaintenance)
    {
        return new PredictiveMaintenanceResponse(
            predictiveMaintenance.Id ?? string.Empty,
            predictiveMaintenance.AssetId ?? string.Empty,
            predictiveMaintenance.AssetName ?? string.Empty,
            predictiveMaintenance.SensorType ?? string.Empty,
            predictiveMaintenance.ThresholdValue,
            predictiveMaintenance.AlertCondition,
            predictiveMaintenance.DataSourceDeviceId,
            predictiveMaintenance.PredictionModelOutput,
            predictiveMaintenance.RiskLevel ?? string.Empty,
            predictiveMaintenance.CreatedBy,
            predictiveMaintenance.CreatedAt,
            predictiveMaintenance.UpdatedBy,
            predictiveMaintenance.UpdatedAt,
            predictiveMaintenance.ClientId,
            predictiveMaintenance.TenantId,
            predictiveMaintenance.Status,
            predictiveMaintenance.IsDeleted
        );
    }
}
