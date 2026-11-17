using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// The Condition and Criticality cards on the asset detail page. Each uses the asset's latest assessment when one
/// exists, otherwise the value entered at registration (with no score or date).
/// </summary>
public sealed record AssetHealthSummaryResponse(
    string AssetRegistrationId,
    AssetConditionSummary Condition,
    AssetCriticalitySummary Criticality)
{
    public static AssetHealthSummaryResponse From(
        AssetRegistration asset,
        AssetConditionAssessment? conditionAssessment,
        AssetCriticalityAssessment? criticalityAssessment)
    {
        var condition = conditionAssessment is not null
            ? new AssetConditionSummary(
                conditionAssessment.OverallCondition,
                conditionAssessment.HealthScore,
                AssetHealthSummarySources.Assessment,
                conditionAssessment.AssessmentDate,
                conditionAssessment.AssessedBy,
                conditionAssessment.Id)
            : new AssetConditionSummary(
                asset.BasicInformation.Condition,
                null,
                asset.BasicInformation.Condition is null ? null : AssetHealthSummarySources.Registration,
                null,
                null,
                null);

        var registeredCriticality = asset.Classification.Criticality ?? asset.BasicInformation.Criticality;
        var criticality = criticalityAssessment is not null
            ? new AssetCriticalitySummary(
                criticalityAssessment.CriticalityLevel,
                criticalityAssessment.CriticalityScore,
                AssetHealthSummarySources.MaxScore,
                AssetHealthSummarySources.Assessment,
                criticalityAssessment.AssessmentDate,
                criticalityAssessment.NextReviewDate,
                criticalityAssessment.AssessedBy,
                criticalityAssessment.Id)
            : new AssetCriticalitySummary(
                registeredCriticality,
                null,
                AssetHealthSummarySources.MaxScore,
                registeredCriticality is null ? null : AssetHealthSummarySources.Registration,
                null,
                null,
                null,
                null);

        return new AssetHealthSummaryResponse(asset.AssetRegistrationId, condition, criticality);
    }
}

/// <param name="Value">"Excellent", "Good", "Fair", "Poor" or "Critical"; null when never set.</param>
/// <param name="HealthScore">0 to 100, only from an assessment.</param>
/// <param name="Source">"Assessment", "Registration", or null when there is no value.</param>
public sealed record AssetConditionSummary(
    string? Value,
    decimal? HealthScore,
    string? Source,
    DateTime? AssessedAt,
    string? AssessedBy,
    string? AssessmentId);

/// <param name="Level">"Low", "Medium", "High" or "Critical"; null when never set.</param>
/// <param name="Score">0 to <paramref name="MaxScore"/>, only from an assessment.</param>
/// <param name="Source">"Assessment", "Registration", or null when there is no value.</param>
public sealed record AssetCriticalitySummary(
    string? Level,
    decimal? Score,
    int MaxScore,
    string? Source,
    DateTime? AssessedAt,
    DateTime? NextReviewDate,
    string? AssessedBy,
    string? AssessmentId);

public static class AssetHealthSummarySources
{
    public const string Assessment = "Assessment";
    public const string Registration = "Registration";
    public const int MaxScore = 100;
}
