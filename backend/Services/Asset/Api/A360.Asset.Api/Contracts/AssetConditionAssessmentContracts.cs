using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>New Condition Assessment form.</summary>
/// <param name="AssessedBy">Defaults to the signed-in user.</param>
public sealed record CreateAssetConditionAssessmentRequest(
    string? AssetRegistrationId,
    DateTime? AssessmentDate,
    string? AssessedBy,
    string? OverallCondition,
    decimal? HealthScore,
    string? PhysicalCondition,
    string? Performance,
    string? Safety,
    string? Remarks,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetConditionAssessment ToEntity(AssetRegistration asset, CurrentUser? currentUser)
    {
        return new AssetConditionAssessment
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetName = asset.BasicInformation.AssetName,
            AssetTag = asset.BasicInformation.AssetTag,
            AssessmentDate = ContractText.DateOnly(AssessmentDate)!.Value,
            AssessedBy = ContractText.FirstNonEmpty(AssessedBy, currentUser?.UserName) ?? string.Empty,
            OverallCondition = AssetConditionRatings.Normalize(OverallCondition)!,
            HealthScore = HealthScore!.Value,
            PhysicalCondition = AssetConditionRatings.Normalize(PhysicalCondition),
            Performance = AssetConditionRatings.Normalize(Performance),
            Safety = AssetConditionRatings.Normalize(Safety),
            Remarks = ContractText.TrimOrNull(Remarks),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

/// <summary>Edit from the Current Condition Assessment panel or a history row. The asset cannot be changed.</summary>
public sealed record UpdateAssetConditionAssessmentRequest(
    DateTime? AssessmentDate,
    string? AssessedBy,
    string? OverallCondition,
    decimal? HealthScore,
    string? PhysicalCondition,
    string? Performance,
    string? Safety,
    string? Remarks)
{
    public void ApplyTo(AssetConditionAssessment assessment, CurrentUser? currentUser)
    {
        assessment.AssessmentDate = ContractText.DateOnly(AssessmentDate)!.Value;
        assessment.AssessedBy = AssessedBy!.Trim();
        assessment.OverallCondition = AssetConditionRatings.Normalize(OverallCondition)!;
        assessment.HealthScore = HealthScore!.Value;
        assessment.PhysicalCondition = AssetConditionRatings.Normalize(PhysicalCondition);
        assessment.Performance = AssetConditionRatings.Normalize(Performance);
        assessment.Safety = AssetConditionRatings.Normalize(Safety);
        assessment.Remarks = ContractText.TrimOrNull(Remarks);
        assessment.UpdatedBy = currentUser?.UserName;
        assessment.UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Ratings used by Overall Condition, Physical Condition, Performance and Safety.</summary>
public static class AssetConditionRatings
{
    public const string Excellent = "Excellent";
    public const string Good = "Good";
    public const string Fair = "Fair";
    public const string Poor = "Poor";
    public const string Critical = "Critical";

    public static IEnumerable<string> All => [Excellent, Good, Fair, Poor, Critical];

    public static bool IsValid(string? rating) =>
        All.Any(value => string.Equals(value, rating?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Stores the rating with its standard spelling, e.g. "good" as "Good"; null when empty.</summary>
    public static string? Normalize(string? rating) =>
        All.FirstOrDefault(value => string.Equals(value, rating?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record AssetConditionAssessmentResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    DateTime AssessmentDate,
    string AssessedBy,
    string OverallCondition,
    decimal HealthScore,
    string? PhysicalCondition,
    string? Performance,
    string? Safety,
    string? Remarks,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetConditionAssessmentResponse FromEntity(AssetConditionAssessment assessment)
    {
        return new AssetConditionAssessmentResponse(
            assessment.Id,
            assessment.AssetRegistrationId,
            assessment.AssetName,
            assessment.AssetTag,
            assessment.AssessmentDate,
            assessment.AssessedBy,
            assessment.OverallCondition,
            assessment.HealthScore,
            assessment.PhysicalCondition,
            assessment.Performance,
            assessment.Safety,
            assessment.Remarks,
            assessment.CreatedBy,
            assessment.CreatedAt,
            assessment.UpdatedBy,
            assessment.UpdatedAt,
            assessment.ClientId,
            assessment.TenantId);
    }
}

public sealed record AssetConditionAssessmentListResponse(
    IReadOnlyCollection<AssetConditionAssessmentResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);
