using A360.Maintenance.Domain.Entities;
using PredictiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PredictiveMaintenance;

namespace A360.Maintenance.Api.Contracts;

/// <summary>Editable predictive model / rule fields shared by create and update.</summary>
public abstract record PredictiveMaintenanceFields
{
    public string? AssetId { get; init; }
    public string? AssetName { get; init; }
    public string? AssetType { get; init; }
    public string? Location { get; init; }
    public string? SensorType { get; init; }
    public double ThresholdValue { get; init; }
    public string? AlertCondition { get; init; }
    public string? DataSourceDeviceId { get; init; }
    public double? LastReading { get; init; }
    public DateTime? LastReadingAt { get; init; }
    public string? PredictionModelOutput { get; init; }
    public string? ModelName { get; init; }
    public string? RiskLevel { get; init; }
    public double? HealthScore { get; init; }
    public string? PredictedIssue { get; init; }
    public DateTime? PredictedFailureDate { get; init; }
    public double? Confidence { get; init; }
    public string? RecommendedAction { get; init; }
    public DateTime? ActualFailureDate { get; init; }
    public string? WorkOrderId { get; init; }
    public List<PredictiveAnomaly>? Anomalies { get; init; }
    public string? Status { get; init; }

    protected void CopyTo(PredictiveMaintenanceEntity prediction)
    {
        prediction.AssetId = AssetId!;
        prediction.AssetName = AssetName ?? string.Empty;
        prediction.AssetType = AssetType;
        prediction.Location = Location;
        prediction.SensorType = SensorType!;
        prediction.ThresholdValue = ThresholdValue;
        prediction.AlertCondition = AlertCondition;
        prediction.DataSourceDeviceId = DataSourceDeviceId;
        prediction.LastReading = LastReading;
        prediction.LastReadingAt = LastReadingAt;
        prediction.PredictionModelOutput = PredictionModelOutput;
        prediction.ModelName = ModelName;
        prediction.RiskLevel = RiskLevel!;
        prediction.HealthScore = HealthScore;
        prediction.PredictedIssue = PredictedIssue;
        prediction.PredictedFailureDate = PredictedFailureDate;
        prediction.Confidence = Confidence;
        prediction.RecommendedAction = RecommendedAction;
        prediction.ActualFailureDate = ActualFailureDate;
        prediction.WorkOrderId = WorkOrderId ?? prediction.WorkOrderId;
        prediction.Anomalies = Anomalies ?? prediction.Anomalies;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            prediction.Status = Status;
        }
    }
}

public sealed record CreatePredictiveMaintenanceRequest : PredictiveMaintenanceFields
{
    public string? CreatedBy { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }

    public PredictiveMaintenanceEntity ToEntity()
    {
        var prediction = new PredictiveMaintenanceEntity
        {
            Status = "Monitoring",
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        CopyTo(prediction);
        return prediction;
    }
}

public sealed record UpdatePredictiveMaintenanceRequest : PredictiveMaintenanceFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(PredictiveMaintenanceEntity prediction)
    {
        CopyTo(prediction);
        prediction.UpdatedBy = UpdatedBy;
        prediction.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record PredictiveMaintenanceResponse
{
    public string Id { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string? AssetType { get; init; }
    public string? Location { get; init; }
    public string SensorType { get; init; } = string.Empty;
    public double ThresholdValue { get; init; }
    public string? AlertCondition { get; init; }
    public string? DataSourceDeviceId { get; init; }
    public double? LastReading { get; init; }
    public DateTime? LastReadingAt { get; init; }
    public string? PredictionModelOutput { get; init; }
    public string? ModelName { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public double? HealthScore { get; init; }
    public string? PredictedIssue { get; init; }
    public DateTime? PredictedFailureDate { get; init; }
    public double? Confidence { get; init; }
    public string? RecommendedAction { get; init; }
    public DateTime? ActualFailureDate { get; init; }
    public string? WorkOrderId { get; init; }
    public List<PredictiveAnomaly> Anomalies { get; init; } = [];
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }
    public string? Status { get; init; }
    public bool IsDeleted { get; init; }

    public static PredictiveMaintenanceResponse FromEntity(PredictiveMaintenanceEntity prediction) => new()
    {
        Id = prediction.Id ?? string.Empty,
        AssetId = prediction.AssetId ?? string.Empty,
        AssetName = prediction.AssetName ?? string.Empty,
        AssetType = prediction.AssetType,
        Location = prediction.Location,
        SensorType = prediction.SensorType ?? string.Empty,
        ThresholdValue = prediction.ThresholdValue,
        AlertCondition = prediction.AlertCondition,
        DataSourceDeviceId = prediction.DataSourceDeviceId,
        LastReading = prediction.LastReading,
        LastReadingAt = prediction.LastReadingAt,
        PredictionModelOutput = prediction.PredictionModelOutput,
        ModelName = prediction.ModelName,
        RiskLevel = prediction.RiskLevel ?? string.Empty,
        HealthScore = prediction.HealthScore,
        PredictedIssue = prediction.PredictedIssue,
        PredictedFailureDate = prediction.PredictedFailureDate,
        Confidence = prediction.Confidence,
        RecommendedAction = prediction.RecommendedAction,
        ActualFailureDate = prediction.ActualFailureDate,
        WorkOrderId = prediction.WorkOrderId,
        Anomalies = (prediction.Anomalies ?? []).OrderByDescending(anomaly => anomaly.DetectedAt).ToList(),
        CreatedBy = prediction.CreatedBy,
        CreatedAt = prediction.CreatedAt,
        UpdatedBy = prediction.UpdatedBy,
        UpdatedAt = prediction.UpdatedAt,
        ClientId = prediction.ClientId,
        TenantId = prediction.TenantId,
        Status = prediction.Status,
        IsDeleted = prediction.IsDeleted
    };
}
