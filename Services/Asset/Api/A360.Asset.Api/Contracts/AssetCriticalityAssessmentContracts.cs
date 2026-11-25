using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

// ----- Requests (Phase 3: Draft -> Pending Approval -> Approved / Rejected) ---------------------------------------------
// The server scores from the template version; any score or weight a client sends is not read.

public sealed record AssessmentRatingRequest(string? CriterionCode, int? Rating, string? Comment);

public sealed record AssessmentRiskRequest(
    string? FailureConsequences,
    bool? AlternativeEquipmentAvailable,
    string? DowntimeImpact,
    string? FinancialLoss,
    string? SafetyEnvironmentalConsequences);

public sealed record AssessmentAttachmentRequest(string? FileName, string? FileUrl, string? FileType, long? FileSize);

/// <param name="OverrideLevel">A level name that differs from the calculated one; needs a Justification.</param>
/// <param name="NextReviewDate">Empty = assessment date + the reassessment interval of the final level.</param>
/// <param name="ConcurrencyVersion">Required when editing (the version the form was loaded at).</param>
public sealed record SaveAssessmentRequest(
    string? AssetRegistrationId,
    string? TemplateId,
    DateTime? AssessmentDate,
    List<AssessmentRatingRequest>? Ratings,
    string? OverrideLevel,
    string? Justification,
    AssessmentRiskRequest? Risk,
    List<AssessmentAttachmentRequest>? Attachments,
    DateTime? NextReviewDate,
    string? Remarks,
    int? ConcurrencyVersion);

public sealed record ScorePreviewRequest(
    string? AssetRegistrationId,
    string? TemplateId,
    List<AssessmentRatingRequest>? Ratings,
    string? OverrideLevel,
    DateTime? AssessmentDate);

/// <param name="Comments">Required to reject.</param>
public sealed record AssessmentDecisionRequest(string? Comments, int? ConcurrencyVersion);

// ----- Responses --------------------------------------------------------------------------------------------------------

public sealed record AssessmentLevelResponse(string? Id, string Name, string? Code, string? Color, int? MinScore, int? MaxScore);

public sealed record AssessmentBreakdownRow(string? CriterionId, string Code, string Name, double Weight, int? Rating, decimal Weighted, string? Comment);

public sealed record AssessmentApprovalInfo(
    string Scenario,
    string? Label,
    IReadOnlyList<string> ApproverRoles,
    string? ApprovalType,
    string? Description);

public sealed record AssessmentScorePreviewResponse(
    decimal Score,
    AssessmentLevelResponse? CalculatedLevel,
    AssessmentLevelResponse? FinalLevel,
    bool IsOverride,
    string? PreviousLevel,
    bool IsLowered,
    bool JustificationRequired,
    DateTime? SuggestedNextReviewDate,
    int? ReviewIntervalMonths,
    AssessmentApprovalInfo? Approval,
    IReadOnlyList<AssessmentBreakdownRow> Breakdown);

public sealed record AssessmentAssetInfo(
    string AssetRegistrationId,
    string? Name,
    string? Tag,
    string? Category,
    string? AssetType,
    string? Site,
    string? Location,
    string? Department,
    string? Owner);

public sealed record AssessmentTrailResponse(string Action, string? By, DateTime At, string? Comments);

public sealed record AssessmentAttachmentResponse(string FileName, string FileUrl, string? FileType, long FileSize, string? UploadedBy, DateTime? UploadedAt);

public sealed record AssessmentRiskResponse(
    string? FailureConsequences,
    bool? AlternativeEquipmentAvailable,
    string? DowntimeImpact,
    string? FinancialLoss,
    string? SafetyEnvironmentalConsequences);

/// <summary>What the signed-in user may do with this assessment right now (the UI shows buttons from this; the API enforces it).</summary>
public sealed record AssessmentActions(bool CanEdit, bool CanSubmit, bool CanCancel, bool CanApprove, bool CanReject, string? Blocked);

public sealed record AssessmentDetailResponse(
    string Id,
    string? AssessmentNumber,
    AssessmentAssetInfo Asset,
    string AssessmentType,
    string Status,
    bool IsCurrent,
    DateTime AssessmentDate,
    string AssessedBy,
    string? TemplateId,
    string? TemplateName,
    int? TemplateVersion,
    IReadOnlyList<AssessmentBreakdownRow> Criteria,
    decimal Score,
    string? CalculatedLevel,
    string FinalLevel,
    string? FinalLevelId,
    string? FinalLevelColor,
    bool IsOverride,
    string? Justification,
    string? PreviousLevel,
    string? PreviousApprovedAssessmentId,
    AssessmentRiskResponse Risk,
    IReadOnlyList<AssessmentAttachmentResponse> Attachments,
    DateTime? NextReviewDate,
    string? Remarks,
    AssessmentApprovalInfo? Approval,
    string? RejectedReason,
    string? SubmittedBy,
    DateTime? SubmittedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    IReadOnlyList<AssessmentTrailResponse> Trail,
    int ConcurrencyVersion,
    bool IsLegacy,
    AssessmentActions Actions,
    bool IsComplete = true);

public sealed record AssessmentVersionResponse(
    string Id,
    string? AssessmentNumber,
    string AssessmentType,
    string Status,
    bool IsCurrent,
    DateTime AssessmentDate,
    decimal Score,
    string Level,
    string AssessedBy,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    int? TemplateVersion,
    string? TemplateName);

public sealed record AssessmentTemplateCriterionResponse(string CriterionId, string Code, string Name, double Weight, string? Description);

public sealed record AssessmentTemplateResponse(
    string Id,
    string Code,
    string Name,
    int Version,
    bool IsDefault,
    IReadOnlyList<AssessmentTemplateCriterionResponse> Criteria);

public sealed record ReviewIntervalResponse(string LevelName, int Months);

public sealed record AssessmentDraftContextResponse(
    AssessmentAssetInfo Asset,
    string? OperationalStatus,
    string? Condition,
    AssessmentCurrentSummary? Current,
    string? OpenAssessmentId,
    string? OpenAssessmentStatus,
    string AssessmentType,
    DateTime AssessmentDate,
    string AssessedBy,
    IReadOnlyList<AssessmentTemplateResponse> Templates,
    string? DefaultTemplateId,
    IReadOnlyList<AssessmentLevelResponse> Levels,
    IReadOnlyList<ReviewIntervalResponse> ReviewIntervals);

public sealed record AssessmentCurrentSummary(string Id, string Level, decimal Score, DateTime AssessmentDate, DateTime? NextReviewDate);
