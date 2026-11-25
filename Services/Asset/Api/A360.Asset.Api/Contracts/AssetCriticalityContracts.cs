using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>The standard criteria and weights the form starts with.</summary>
public static class AssetCriticalityCriteria
{
    public const decimal MinScore = 1;
    public const decimal MaxScore = 5;

    public static readonly IReadOnlyCollection<AssetCriticalityCriterionDefault> Defaults =
    [
        new("Operational Impact", 25),
        new("Safety Impact", 20),
        new("Financial Impact", 20),
        new("Compliance / Regulatory", 15),
        new("Environmental Impact", 10),
        new("Reputation Impact", 10)
    ];
}

public sealed record AssetCriticalityCriterionDefault(string Name, decimal Weight);

/// <summary>Likelihood and Consequence ratings, the two axes of the Criticality Matrix.</summary>
public static class AssetRiskRatings
{
    public static IEnumerable<string> All => ["Very Low", "Low", "Medium", "High", "Very High"];

    public static bool IsValid(string? rating) =>
        All.Any(value => string.Equals(value, rating?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? rating) =>
        All.FirstOrDefault(value => string.Equals(value, rating?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Criticality levels and the score each one starts at.</summary>
public static class AssetCriticalityLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public const decimal CriticalFrom = 80;
    public const decimal HighFrom = 60;
    public const decimal MediumFrom = 40;

    public static IEnumerable<string> All => [Low, Medium, High, Critical];

    public static bool IsValid(string? level) =>
        All.Any(value => string.Equals(value, level?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? level) =>
        All.FirstOrDefault(value => string.Equals(value, level?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string FromScore(decimal score) => score switch
    {
        >= CriticalFrom => Critical,
        >= HighFrom => High,
        >= MediumFrom => Medium,
        _ => Low
    };
}

public sealed record AssetCriticalityCriterionResponse(
    string Name,
    decimal Weight,
    decimal Score,
    decimal WeightedScore,
    string Justification)
{
    public static AssetCriticalityCriterionResponse FromEntity(AssetCriticalityCriterion criterion)
    {
        return new AssetCriticalityCriterionResponse(
            criterion.Name,
            criterion.Weight,
            criterion.Score,
            criterion.WeightedScore,
            criterion.Justification);
    }
}

/// <param name="AssetStatus">The asset's status when assessed, for the list's Status column.</param>
public sealed record AssetCriticalityResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string? Category,
    string? Location,
    string? AssetStatus,
    bool IsCurrent,
    DateTime AssessmentDate,
    DateTime? ValidUntil,
    DateTime? NextReviewDate,
    string AssessedBy,
    string ApprovedBy,
    string? Likelihood,
    string? Consequence,
    IReadOnlyCollection<AssetCriticalityCriterionResponse> Criteria,
    decimal CriticalityScore,
    string CriticalityLevel,
    string? Remarks,
    string? AssessmentMethod,
    string? ReviewFrequency,
    string? Department,
    string? AssetGroup,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string ApprovalStatus = AssetCriticalityApprovalStatuses.Approved)
{
    public static AssetCriticalityResponse FromEntity(AssetCriticalityAssessment assessment)
    {
        return new AssetCriticalityResponse(
            assessment.Id,
            assessment.AssetRegistrationId,
            assessment.AssetName,
            assessment.AssetTag,
            assessment.Category,
            assessment.Location,
            assessment.AssetStatus,
            assessment.IsCurrent,
            assessment.AssessmentDate,
            assessment.ValidUntil,
            assessment.NextReviewDate,
            assessment.AssessedBy,
            assessment.ApprovedBy,
            assessment.Likelihood,
            assessment.Consequence,
            assessment.Criteria.Select(AssetCriticalityCriterionResponse.FromEntity).ToList(),
            assessment.CriticalityScore,
            assessment.CriticalityLevel,
            assessment.Remarks,
            assessment.AssessmentMethod,
            assessment.ReviewFrequency,
            assessment.Department,
            assessment.AssetGroup,
            assessment.CreatedBy,
            assessment.CreatedAt,
            assessment.UpdatedBy,
            assessment.UpdatedAt,
            assessment.ClientId,
            assessment.TenantId,
            assessment.ApprovalStatus ?? AssetCriticalityApprovalStatuses.Approved);
    }
}

public sealed record AssetCriticalityListResponse(
    IReadOnlyCollection<AssetCriticalityResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>Dropdown values and default criteria for the Assess Asset form.</summary>
public sealed record AssetCriticalityOptionsResponse(
    IEnumerable<string> LikelihoodRatings,
    IEnumerable<string> ConsequenceRatings,
    IEnumerable<string> CriticalityLevels,
    IReadOnlyCollection<AssetCriticalityCriterionDefault> Criteria);
