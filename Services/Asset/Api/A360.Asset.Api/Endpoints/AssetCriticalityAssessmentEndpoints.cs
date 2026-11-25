using MongoDB.Driver;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Security;
using A360.Asset.Api.Services;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Asset Criticality assessments, Phase 3: Draft -> Pending Approval -> Approved / Rejected. Scoring happens only here;
/// only an Approved assessment changes the asset's official criticality, and an approved assessment is never edited
/// (a reassessment is a new one).
/// </summary>
public static class AssetCriticalityAssessmentEndpoints
{
    private const string EntityType = "AssetCriticalityAssessment";
    private const long MaxAttachmentBytes = 15L * 1024 * 1024;
    private static readonly string[] Editable = [AssetCriticalityApprovalStatuses.Draft, AssetCriticalityApprovalStatuses.Rejected];
    private static readonly string[] Pending = [AssetCriticalityApprovalStatuses.PendingApproval, AssetCriticalityApprovalStatuses.Submitted];

    public static void MapAssetCriticalityAssessmentEndpoints(this RouteGroupBuilder group)
    {
        const string Assess = AssetCriticalityPermissions.Assess;
        const string Approve = AssetCriticalityPermissions.Approve;

        group.MapPost("/score-preview", ScorePreviewAsync).RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapPost("/attachments", UploadAttachmentAsync).DisableAntiforgery().RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapPost("/assessments", CreateAsync).RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapGet("/assessments/{id}", GetAsync);
        group.MapPut("/assessments/{id}", UpdateAsync).RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapDelete("/assessments/{id}", CancelAsync).RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapPost("/assessments/{id}/submit", SubmitAsync).RequireCriticalityPermission(Assess, "assess asset criticality");
        group.MapPost("/assessments/{id}/approve", ApproveAsync).RequireCriticalityPermission(Approve, "approve asset criticality");
        group.MapPost("/assessments/{id}/reject", RejectAsync).RequireCriticalityPermission(Approve, "approve asset criticality");
        group.MapGet("/assets/{assetRegistrationId}/versions", GetVersionsAsync);
        group.MapGet("/assets/{assetRegistrationId}/draft-context", GetDraftContextAsync);
    }

    // ----- Draft context and live preview ---------------------------------------------------------------------------

    private static async Task<IResult> GetDraftContextAsync(
        string assetRegistrationId,
        IAssetRegistrationRepository assets,
        IAssetCriticalityAssessmentRepository repository,
        AssetCriticalityTemplateRepository templates,
        AssetCriticalityCriteriaRepository criteria,
        AssetCriticalityReassessmentRuleRepository reassessmentRules,
        ICriticalityLevelClient levelClient,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var asset = await assets.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null || asset.IsDeleted)
        {
            return Results.NotFound(new { message = $"Asset '{assetRegistrationId}' was not found." });
        }

        var user = currentUserAccessor.GetCurrentUser();
        var current = await repository.GetCurrentAsync(asset.AssetRegistrationId, cancellationToken);
        var open = await repository.GetOpenAsync(asset.AssetRegistrationId, cancellationToken);
        var latest = await templates.ListLatestAsync(user?.ClientId, cancellationToken);
        var descriptions = (await criteria.ListAsync(user?.ClientId, true, cancellationToken))
            .ToDictionary(c => c.Code, c => c.Description, StringComparer.OrdinalIgnoreCase);
        var levels = await levelClient.GetLevelsAsync(cancellationToken);
        var rules = await reassessmentRules.ListAsync(user?.ClientId, cancellationToken);

        var templateRows = latest
            .Where(t => !string.Equals(t.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            .Select(t => ToTemplate(t, descriptions))
            .ToList();

        return Results.Ok(new AssessmentDraftContextResponse(
            ToAssetInfo(asset),
            asset.BasicInformation.Status,
            asset.BasicInformation.Condition,
            current is null ? null : new AssessmentCurrentSummary(current.Id, current.CriticalityLevel, current.CriticalityScore, current.AssessmentDate, current.NextReviewDate),
            open?.Id,
            open?.ApprovalStatus,
            current is null ? AssetCriticalityAssessmentTypes.Initial : AssetCriticalityAssessmentTypes.Reassessment,
            DateTime.UtcNow.Date,
            user?.UserName ?? string.Empty,
            templateRows,
            templateRows.FirstOrDefault(t => t.IsDefault)?.Id ?? templateRows.FirstOrDefault()?.Id,
            levels.Select(ToLevel).ToList(),
            rules.Select(r => new ReviewIntervalResponse(r.LevelName, r.ReviewIntervalMonths)).ToList()));
    }

    private static async Task<IResult> ScorePreviewAsync(
        ScorePreviewRequest request,
        IAssetCriticalityAssessmentRepository repository,
        AssetCriticalityTemplateRepository templates,
        AssetCriticalityApprovalRuleRepository approvalRules,
        AssetCriticalityReassessmentRuleRepository reassessmentRules,
        ICriticalityLevelClient levelClient,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var template = string.IsNullOrWhiteSpace(request.TemplateId) ? null : await templates.GetByIdAsync(request.TemplateId.Trim(), cancellationToken);
        if (template is null || template.IsDeleted)
        {
            return Problem("TemplateId", "Choose a template");
        }

        var errors = new Dictionary<string, string[]>();
        var ratings = ReadRatings(request.Ratings, template, errors);
        var levels = await levelClient.GetLevelsAsync(cancellationToken);
        var scored = ratings is null ? null : CriticalityScoring.Score(template, ratings, levels);
        if (scored is null || scored.Level is null)
        {
            return Results.ValidationProblem(errors.Count > 0 ? errors : new Dictionary<string, string[]> { ["Ratings"] = ["Rate every criterion from 1 to 5"] });
        }

        var user = currentUserAccessor.GetCurrentUser();
        var previous = string.IsNullOrWhiteSpace(request.AssetRegistrationId)
            ? null
            : await repository.GetCurrentAsync(request.AssetRegistrationId.Trim(), cancellationToken);

        var final = ResolveFinalLevel(scored.Level, request.OverrideLevel, levels, errors);
        if (final is null)
        {
            return Results.ValidationProblem(errors);
        }

        var date = ContractText.DateOnly(request.AssessmentDate) ?? DateTime.UtcNow.Date;
        var months = (await reassessmentRules.GetByLevelAsync(user?.ClientId, final.Name, cancellationToken))?.ReviewIntervalMonths;
        var scenario = CriticalityScoring.Scenario(final.Name, previous?.CriticalityLevel);
        var rule = await approvalRules.GetByScenarioAsync(user?.ClientId, scenario, cancellationToken);
        var lowered = CriticalityAccess.IsReduction(previous?.CriticalityLevel, final.Name);
        var isOverride = !string.Equals(final.Name, scored.Level.Name, StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new AssessmentScorePreviewResponse(
            scored.Score,
            ToLevel(scored.Level),
            ToLevel(final),
            isOverride,
            previous?.CriticalityLevel,
            lowered,
            isOverride || lowered,
            months is null ? null : date.AddMonths(months.Value),
            months,
            ToApproval(scenario, rule),
            scored.Breakdown.Select(ToBreakdown).ToList()));
    }

    // ----- Create / edit / cancel -----------------------------------------------------------------------------------

    private static async Task<IResult> CreateAsync(
        SaveAssessmentRequest request,
        AssessmentServices s,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var user = s.Users.GetCurrentUser();
        var assetId = request.AssetRegistrationId?.Trim();
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Problem("AssetRegistrationId", "Choose an asset");
        }

        var asset = await s.Assets.GetByAssetRegistrationIdAsync(assetId, cancellationToken);
        if (asset is null || asset.IsDeleted)
        {
            return Problem("AssetRegistrationId", $"Asset '{assetId}' was not found");
        }

        var open = await s.Repository.GetOpenAsync(asset.AssetRegistrationId, cancellationToken);
        if (open is not null)
        {
            return Results.Conflict(new { message = $"Asset '{assetId}' already has an assessment in progress ({open.AssessmentNumber ?? open.Id}, {open.ApprovalStatus}).", openAssessmentId = open.Id });
        }

        var template = string.IsNullOrWhiteSpace(request.TemplateId) ? null : await s.Templates.GetByIdAsync(request.TemplateId.Trim(), cancellationToken);
        if (template is null || template.IsDeleted)
        {
            return Problem("TemplateId", "Choose a template");
        }

        if (!template.IsLatest || string.Equals(template.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return Problem("TemplateId", "This template version is no longer in use; choose the current one");
        }

        var current = await s.Repository.GetCurrentAsync(asset.AssetRegistrationId, cancellationToken);
        var assessment = new AssetCriticalityAssessment
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            ApprovalStatus = AssetCriticalityApprovalStatuses.Draft,
            AssessmentNumber = await s.Repository.NextNumberAsync(cancellationToken),
            CreatedBy = user?.UserName,
            CreatedByUserId = user?.UserId ?? user?.Id,
            CreatedAt = DateTime.UtcNow,
            ClientId = user?.ClientId,
            TenantId = user?.TenantId,
            IsDeleted = false,
            IsCurrent = false,
            AssessedBy = user?.UserName ?? string.Empty,
            AssessedByUserId = user?.UserId ?? user?.Id,
            ConcurrencyVersion = 1
        };

        var failure = await ApplyAsync(assessment, request, template, asset, current, s, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Trail(assessment, "Created", user);
        await s.Repository.CreateAsync(assessment, cancellationToken);
        await s.Events.LogAsync(EntityType, assessment.Id, Describe(assessment, "draft saved"), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-criticality/assessments/{assessment.Id}", await DetailAsync(assessment, s, http, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(
        string id,
        SaveAssessmentRequest request,
        AssessmentServices s,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return NotFound(id);
        }

        var locked = EnsureEditable(assessment);
        if (locked is not null)
        {
            return locked;
        }

        var user = s.Users.GetCurrentUser();
        if (!CanChangeDraft(assessment, user, http))
        {
            return CriticalityAccess.Forbidden("Only the assessor who created this assessment can edit it.");
        }

        if (request.ConcurrencyVersion is null)
        {
            return Problem("ConcurrencyVersion", "Reload the assessment before saving");
        }

        if (request.ConcurrencyVersion != assessment.ConcurrencyVersion)
        {
            return Results.Conflict(new { message = "This assessment was changed by someone else. Reload it and try again." });
        }

        var asset = await s.Assets.GetByAssetRegistrationIdAsync(assessment.AssetRegistrationId, cancellationToken);
        if (asset is null)
        {
            return NotFound(id);
        }

        // The asset cannot be changed on an existing assessment; the template can be, but only to the version in use.
        var template = string.IsNullOrWhiteSpace(request.TemplateId) ? null : await s.Templates.GetByIdAsync(request.TemplateId.Trim(), cancellationToken);
        if (template is null || template.IsDeleted)
        {
            return Problem("TemplateId", "Choose a template");
        }

        if (template.Id != assessment.TemplateId && (!template.IsLatest || string.Equals(template.Status, "Inactive", StringComparison.OrdinalIgnoreCase)))
        {
            return Problem("TemplateId", "This template version is no longer in use; choose the current one");
        }

        var expected = assessment.ConcurrencyVersion;
        var current = await s.Repository.GetCurrentAsync(asset.AssetRegistrationId, cancellationToken);
        var failure = await ApplyAsync(assessment, request, template, asset, current, s, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        assessment.UpdatedBy = user?.UserName;
        assessment.UpdatedAt = DateTime.UtcNow;
        Trail(assessment, "Saved", user);
        if (!await s.Repository.ReplaceIfAsync(assessment, expected, Editable, cancellationToken))
        {
            return Results.Conflict(new { message = "This assessment was changed or submitted by someone else. Reload it." });
        }

        await s.Events.LogAsync(EntityType, assessment.Id, Describe(assessment, "draft updated"), EventAction.Updated, cancellationToken);
        return Results.Ok(await DetailAsync(assessment, s, http, cancellationToken));
    }

    private static async Task<IResult> CancelAsync(string id, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return NotFound(id);
        }

        var locked = EnsureEditable(assessment);
        if (locked is not null)
        {
            return locked;
        }

        var user = s.Users.GetCurrentUser();
        if (!CanChangeDraft(assessment, user, http))
        {
            return CriticalityAccess.Forbidden("Only the assessor who created this assessment can cancel it.");
        }

        var updated = await s.Repository.TransitionAsync(id, Editable, null,
            Builders<AssetCriticalityAssessment>.Update
                .Set(a => a.ApprovalStatus, AssetCriticalityApprovalStatuses.Cancelled)
                .Set(a => a.UpdatedBy, user?.UserName)
                .Set(a => a.UpdatedAt, DateTime.UtcNow)
                .Push(a => a.Trail, TrailEntry("Cancelled", user, null)), cancellationToken);
        if (updated is null)
        {
            return Results.Conflict(new { message = "This assessment was changed or submitted by someone else." });
        }

        await s.Events.LogAsync(EntityType, id, Describe(updated, "cancelled"), EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // ----- Workflow --------------------------------------------------------------------------------------------------

    private static async Task<IResult> SubmitAsync(string id, AssessmentDecisionRequest? body, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return NotFound(id);
        }

        var locked = EnsureEditable(assessment);
        if (locked is not null)
        {
            return locked;
        }

        var user = s.Users.GetCurrentUser();
        if (!CanChangeDraft(assessment, user, http))
        {
            return CriticalityAccess.Forbidden("Only the assessor who created this assessment can submit it.");
        }

        if (body?.ConcurrencyVersion is not null && body.ConcurrencyVersion != assessment.ConcurrencyVersion)
        {
            return Results.Conflict(new { message = "This assessment was changed by someone else. Reload it and try again." });
        }

        // The asset may have been (re)assessed since the draft was saved: re-read the approved one and the thresholds.
        var current = await s.Repository.GetCurrentAsync(assessment.AssetRegistrationId, cancellationToken);
        var finalLevel = assessment.CriticalityLevel;
        var lowered = CriticalityAccess.IsReduction(current?.CriticalityLevel, finalLevel);
        if ((assessment.IsOverride || lowered) && string.IsNullOrWhiteSpace(assessment.Justification))
        {
            return Problem("Justification", assessment.IsOverride ? "A justification is required for a level override" : "A justification is required when the criticality is lowered");
        }

        var scenario = CriticalityScoring.Scenario(finalLevel, current?.CriticalityLevel);
        var rule = await s.ApprovalRules.GetByScenarioAsync(user?.ClientId, scenario, cancellationToken);
        var now = DateTime.UtcNow;
        var updated = await s.Repository.TransitionAsync(id, Editable, assessment.ConcurrencyVersion,
            Builders<AssetCriticalityAssessment>.Update
                .Set(a => a.ApprovalStatus, AssetCriticalityApprovalStatuses.PendingApproval)
                .Set(a => a.SubmittedBy, user?.UserName)
                .Set(a => a.SubmittedByUserId, user?.UserId ?? user?.Id)
                .Set(a => a.SubmittedAt, now)
                .Set(a => a.PreviousApprovedAssessmentId, current?.Id)
                .Set(a => a.PreviousLevel, current?.CriticalityLevel)
                .Set(a => a.AssessmentType, current is null ? AssetCriticalityAssessmentTypes.Initial : AssetCriticalityAssessmentTypes.Reassessment)
                .Set(a => a.ApprovalScenario, scenario)
                .Set(a => a.ApproverRoles, rule?.ApproverRoles ?? [])
                .Set(a => a.ApprovalType, rule?.ApprovalType)
                .Set(a => a.RejectedReason, (string?)null)
                .Set(a => a.UpdatedBy, user?.UserName)
                .Set(a => a.UpdatedAt, now)
                .Push(a => a.Trail, TrailEntry("Submitted", user, null)), cancellationToken);
        if (updated is null)
        {
            return Results.Conflict(new { message = "This assessment was changed or already submitted. Reload it." });
        }

        await s.Events.LogAsync(EntityType, id, Describe(updated, "submitted for approval"), EventAction.Updated, cancellationToken);
        return Results.Ok(await DetailAsync(updated, s, http, cancellationToken));
    }

    private static async Task<IResult> ApproveAsync(string id, AssessmentDecisionRequest? body, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return NotFound(id);
        }

        var denied = CheckDecision(assessment, s.Users.GetCurrentUser(), http, "approve");
        if (denied is not null)
        {
            return denied;
        }

        // A newer assessment of this asset was approved after this one was prepared: this one is out of date.
        var current = await s.Repository.GetCurrentAsync(assessment.AssetRegistrationId, cancellationToken);
        if (!string.Equals(current?.Id, assessment.PreviousApprovedAssessmentId, StringComparison.Ordinal))
        {
            return Results.Conflict(new { message = "A newer assessment of this asset has already been approved. Reject this one and reassess the asset." });
        }

        var user = s.Users.GetCurrentUser();
        var now = DateTime.UtcNow;
        var updated = await s.Repository.TransitionAsync(id, Pending, body?.ConcurrencyVersion,
            Builders<AssetCriticalityAssessment>.Update
                .Set(a => a.ApprovalStatus, AssetCriticalityApprovalStatuses.Approved)
                .Set(a => a.IsCurrent, true)
                .Set(a => a.ApprovedBy, user?.UserName ?? string.Empty)
                .Set(a => a.ApprovedByUserId, user?.UserId ?? user?.Id)
                .Set(a => a.ApprovedAt, now)
                .Set(a => a.UpdatedBy, user?.UserName)
                .Set(a => a.UpdatedAt, now)
                .Push(a => a.Trail, TrailEntry("Approved", user, ContractText.TrimOrNull(body?.Comments))), cancellationToken);
        if (updated is null)
        {
            return Results.Conflict(new { message = "This assessment was already decided or changed. Reload it." });
        }

        // Publish: only now does the asset's official criticality change.
        await s.Repository.ClearCurrentExceptAsync(updated.AssetRegistrationId, updated.Id, cancellationToken);
        await s.History.CreateAsync(new AssetCriticalityHistory
        {
            AssetRegistrationId = updated.AssetRegistrationId,
            AssessmentId = updated.Id,
            FromLevel = updated.PreviousLevel,
            ToLevel = updated.CriticalityLevel,
            ToLevelId = updated.CriticalityLevelId,
            Score = updated.CriticalityScore,
            EffectiveFrom = now,
            ApprovedBy = user?.UserName,
            ChangeReason = updated.Justification,
            CreatedBy = user?.UserName,
            CreatedAt = now,
            ClientId = updated.ClientId,
            TenantId = updated.TenantId
        }, cancellationToken);
        await s.History.SyncAssetAsync(updated.AssetRegistrationId, updated.CriticalityLevel, updated.CriticalityLevelId, updated.NextReviewDate, updated.CriticalityScore, updated.Id, cancellationToken);
        await s.Events.LogAsync(EntityType, id,
            $"{updated.AssetRegistrationId} - approved {updated.PreviousLevel ?? "unassessed"} -> {updated.CriticalityLevel} ({updated.CriticalityScore}/100) by {user?.UserName}",
            EventAction.Updated, cancellationToken);

        return Results.Ok(await DetailAsync(updated, s, http, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(string id, AssessmentDecisionRequest? body, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var comments = ContractText.TrimOrNull(body?.Comments);
        if (comments is null)
        {
            return Problem("Comments", "A comment is required to reject");
        }

        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return NotFound(id);
        }

        var denied = CheckDecision(assessment, s.Users.GetCurrentUser(), http, "reject");
        if (denied is not null)
        {
            return denied;
        }

        var user = s.Users.GetCurrentUser();
        var now = DateTime.UtcNow;
        var updated = await s.Repository.TransitionAsync(id, Pending, body?.ConcurrencyVersion,
            Builders<AssetCriticalityAssessment>.Update
                .Set(a => a.ApprovalStatus, AssetCriticalityApprovalStatuses.Rejected)
                .Set(a => a.RejectedReason, comments)
                .Set(a => a.UpdatedBy, user?.UserName)
                .Set(a => a.UpdatedAt, now)
                .Push(a => a.Trail, TrailEntry("Rejected", user, comments)), cancellationToken);
        if (updated is null)
        {
            return Results.Conflict(new { message = "This assessment was already decided or changed. Reload it." });
        }

        await s.Events.LogAsync(EntityType, id, Describe(updated, "rejected: " + comments), EventAction.Updated, cancellationToken);
        return Results.Ok(await DetailAsync(updated, s, http, cancellationToken));
    }

    // ----- Reads -----------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetAsync(string id, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var assessment = await s.Repository.GetByIdAsync(id, cancellationToken);
        return assessment is null || assessment.IsDeleted
            ? NotFound(id)
            : Results.Ok(await DetailAsync(assessment, s, http, cancellationToken));
    }

    private static async Task<IResult> GetVersionsAsync(string assetRegistrationId, IAssetCriticalityAssessmentRepository repository, CancellationToken cancellationToken)
    {
        var versions = await repository.GetVersionsAsync(assetRegistrationId.Trim(), cancellationToken);
        return Results.Ok(versions.Select(a => new AssessmentVersionResponse(
            a.Id,
            a.AssessmentNumber,
            a.AssessmentType ?? AssetCriticalityAssessmentTypes.Initial,
            Status(a),
            a.IsCurrent,
            a.AssessmentDate,
            a.CriticalityScore,
            a.CriticalityLevel,
            a.AssessedBy,
            string.IsNullOrWhiteSpace(a.ApprovedBy) ? null : a.ApprovedBy,
            a.ApprovedAt,
            a.TemplateVersion,
            a.TemplateName ?? a.TemplateCode)).ToList());
    }

    private static async Task<IResult> UploadAttachmentAsync(IFormFile? file, IMediaUploadClient media, ICurrentUserAccessor users, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem("file", "Choose a file to upload");
        }

        if (file.Length > MaxAttachmentBytes)
        {
            return Problem("file", "The file is larger than 15 MB");
        }

        var result = await media.UploadAsync(file, "asset-criticality", cancellationToken);
        if (string.IsNullOrWhiteSpace(result.Url))
        {
            return Results.Json(new { message = result.Error ?? "The file could not be uploaded." }, statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new AssessmentAttachmentResponse(file.FileName, result.Url, file.ContentType, file.Length, users.GetCurrentUser()?.UserName, DateTime.UtcNow));
    }

    // ----- Shared: validate + score + fill the entity ---------------------------------------------------------------

    /// <summary>Validates the request and fills the editable fields and the score from the template. Null on success.</summary>
    private static async Task<IResult?> ApplyAsync(
        AssetCriticalityAssessment assessment,
        SaveAssessmentRequest request,
        AssetCriticalityTemplate template,
        AssetRegistration asset,
        AssetCriticalityAssessment? current,
        AssessmentServices s,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var user = s.Users.GetCurrentUser();
        var levels = await s.Levels.GetLevelsAsync(cancellationToken);

        var ratings = ReadRatings(request.Ratings, template, errors);
        var scored = ratings is null ? null : CriticalityScoring.Score(template, ratings, levels);
        if (scored?.Level is null)
        {
            return Results.ValidationProblem(errors.Count > 0 ? errors : new Dictionary<string, string[]> { ["Ratings"] = ["Rate every criterion from 1 to 5"] });
        }

        var final = ResolveFinalLevel(scored.Level, request.OverrideLevel, levels, errors);
        if (final is null)
        {
            return Results.ValidationProblem(errors);
        }

        var isOverride = !string.Equals(final.Name, scored.Level.Name, StringComparison.OrdinalIgnoreCase);
        var lowered = CriticalityAccess.IsReduction(current?.CriticalityLevel, final.Name);
        var justification = ContractText.TrimOrNull(request.Justification);
        if ((isOverride || lowered) && justification is null)
        {
            errors["Justification"] = [isOverride ? "A justification is required for a level override" : "A justification is required when the criticality is lowered"];
        }

        if (justification?.Length > 1000)
        {
            errors["Justification"] = ["Justification cannot be longer than 1000 characters"];
        }

        var date = ContractText.DateOnly(request.AssessmentDate) ?? DateTime.UtcNow.Date;
        if (date > DateTime.UtcNow.Date)
        {
            errors["AssessmentDate"] = ["The assessment date cannot be in the future"];
        }

        var nextReview = ContractText.DateOnly(request.NextReviewDate);
        if (nextReview is null)
        {
            var months = (await s.ReassessmentRules.GetByLevelAsync(user?.ClientId, final.Name, cancellationToken))?.ReviewIntervalMonths ?? 12;
            nextReview = date.AddMonths(months);
        }
        else if (nextReview < date)
        {
            errors["NextReviewDate"] = ["The next review date cannot be before the assessment date"];
        }

        var risk = request.Risk;
        LimitText(errors, "Risk.FailureConsequences", risk?.FailureConsequences, 2000);
        LimitText(errors, "Risk.DowntimeImpact", risk?.DowntimeImpact, 1000);
        LimitText(errors, "Risk.FinancialLoss", risk?.FinancialLoss, 1000);
        LimitText(errors, "Risk.SafetyEnvironmentalConsequences", risk?.SafetyEnvironmentalConsequences, 2000);
        LimitText(errors, "Remarks", request.Remarks, 2000);

        var attachments = (request.Attachments ?? []).Where(a => !string.IsNullOrWhiteSpace(a.FileUrl)).ToList();
        if (attachments.Count > 20)
        {
            errors["Attachments"] = ["At most 20 documents can be attached"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var comments = (request.Ratings ?? []).Where(r => r.CriterionCode is not null)
            .ToDictionary(r => r.CriterionCode!.Trim(), r => ContractText.TrimOrNull(r.Comment), StringComparer.OrdinalIgnoreCase);

        assessment.AssetName = asset.BasicInformation.AssetName;
        assessment.AssetTag = asset.BasicInformation.AssetTag;
        assessment.Category = asset.Classification.Category;
        assessment.Location = asset.LocationAssignment.Location.LocationPath;
        assessment.AssetStatus = asset.BasicInformation.Status;
        assessment.Department = ContractText.TrimOrNull(asset.Classification.Department);
        assessment.AssessmentDate = date;
        assessment.NextReviewDate = nextReview;
        assessment.ValidUntil = nextReview;
        assessment.TemplateId = template.Id;
        assessment.TemplateCode = template.Code;
        assessment.TemplateName = template.Name;
        assessment.TemplateVersion = template.Version;
        assessment.Criteria = scored.Breakdown.Select(row => new AssetCriticalityCriterion
        {
            CriterionId = row.Criterion.CriterionId,
            Code = row.Criterion.Code,
            Name = row.Criterion.Name,
            Weight = (decimal)row.Criterion.Weight,
            Rating = row.Rating,
            Score = row.Rating,
            WeightedScore = row.Weighted,
            Justification = comments.GetValueOrDefault(row.Criterion.Code) ?? string.Empty
        }).ToList();
        assessment.CriticalityScore = scored.Score;
        assessment.CalculatedLevel = scored.Level.Name;
        assessment.CriticalityLevel = final.Name;
        assessment.CriticalityLevelId = final.Id;
        assessment.CriticalityLevelCode = final.Code;
        assessment.IsOverride = isOverride;
        assessment.Justification = justification;
        assessment.PreviousApprovedAssessmentId = current?.Id;
        assessment.PreviousLevel = current?.CriticalityLevel;
        assessment.AssessmentType = current is null ? AssetCriticalityAssessmentTypes.Initial : AssetCriticalityAssessmentTypes.Reassessment;
        assessment.Remarks = ContractText.TrimOrNull(request.Remarks);
        assessment.Risk = new AssetCriticalityRisk
        {
            FailureConsequences = ContractText.TrimOrNull(risk?.FailureConsequences),
            AlternativeEquipmentAvailable = risk?.AlternativeEquipmentAvailable,
            DowntimeImpact = ContractText.TrimOrNull(risk?.DowntimeImpact),
            FinancialLoss = ContractText.TrimOrNull(risk?.FinancialLoss),
            SafetyEnvironmentalConsequences = ContractText.TrimOrNull(risk?.SafetyEnvironmentalConsequences)
        };
        assessment.Attachments = attachments.Select(a => new AssetCriticalityAttachment
        {
            FileName = ContractText.TrimOrNull(a.FileName) ?? "document",
            FileUrl = a.FileUrl!.Trim(),
            FileType = ContractText.TrimOrNull(a.FileType),
            FileSize = a.FileSize ?? 0,
            UploadedBy = user?.UserName,
            UploadedAt = DateTime.UtcNow
        }).ToList();
        return null;
    }

    /// <summary>One integer rating (1-5) for each criterion of the template, no unknown or repeated codes.</summary>
    private static Dictionary<string, int>? ReadRatings(List<AssessmentRatingRequest>? sent, AssetCriticalityTemplate template, Dictionary<string, string[]> errors)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var known = template.Criteria.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sent ?? [])
        {
            var code = row.CriterionCode?.Trim();
            if (string.IsNullOrEmpty(code) || !known.Contains(code))
            {
                errors["Ratings"] = [$"'{row.CriterionCode}' is not a criterion of this template"];
                return null;
            }

            if (row.Rating is null or < CriticalityScoring.MinRating or > CriticalityScoring.MaxRating)
            {
                errors[$"Ratings.{code}"] = ["Rating must be a whole number from 1 to 5"];
                return null;
            }

            if (!result.TryAdd(code, row.Rating.Value))
            {
                errors["Ratings"] = [$"Criterion '{code}' is rated twice"];
                return null;
            }
        }

        var missing = template.Criteria.Where(c => !result.ContainsKey(c.Code)).Select(c => c.Name).ToList();
        if (missing.Count > 0)
        {
            errors["Ratings"] = [$"Rate every criterion (missing: {string.Join(", ", missing)})"];
            return null;
        }

        // Re-key by the template's own code spelling.
        return template.Criteria.ToDictionary(c => c.Code, c => result[c.Code], StringComparer.OrdinalIgnoreCase);
    }

    private static CriticalityLevelInfo? ResolveFinalLevel(CriticalityLevelInfo calculated, string? overrideLevel, IReadOnlyList<CriticalityLevelInfo> levels, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(overrideLevel))
        {
            return calculated;
        }

        var level = levels.FirstOrDefault(l => string.Equals(l.Name, overrideLevel.Trim(), StringComparison.OrdinalIgnoreCase));
        if (level is null)
        {
            errors["OverrideLevel"] = [$"'{overrideLevel}' is not a criticality level"];
        }

        return level;
    }

    // ----- Permission rules ------------------------------------------------------------------------------------------

    private static IResult? EnsureEditable(AssetCriticalityAssessment assessment)
    {
        var status = Status(assessment);
        if (status == AssetCriticalityApprovalStatuses.Approved)
        {
            return Results.Conflict(new { message = "An approved assessment cannot be changed. Start a reassessment instead." });
        }

        return Editable.Contains(status)
            ? null
            : Results.Conflict(new { message = $"An assessment that is {status} cannot be edited." });
    }

    /// <summary>The assessor (creator) edits, submits and cancels a draft; Configure (administrators) may also.</summary>
    private static bool CanChangeDraft(AssetCriticalityAssessment assessment, CurrentUser? user, HttpContext http) =>
        IsSameUser(user, assessment.CreatedByUserId, assessment.CreatedBy)
        || IsSameUser(user, assessment.AssessedByUserId, assessment.AssessedBy)
        || http.Permissions().Contains(AssetCriticalityPermissions.Configure);

    /// <summary>Approve / reject: pending only, the level's approval permission, and never the assessor or submitter.</summary>
    private static IResult? CheckDecision(AssetCriticalityAssessment assessment, CurrentUser? user, HttpContext http, string verb)
    {
        var status = Status(assessment);
        if (!Pending.Contains(status))
        {
            return Results.Conflict(new { message = $"Only an assessment waiting for approval can be {verb}d; this one is {status}." });
        }

        if (!CriticalityAccess.CanApprove(http.Permissions(), assessment.CriticalityLevel, assessment.PreviousLevel))
        {
            return CriticalityAccess.Forbidden("Your role can't " + verb + " this assessment (Critical and lowered levels need Approve in Assets).");
        }

        return IsAssessor(assessment, user)
            ? CriticalityAccess.Forbidden($"You assessed this asset, so you can't {verb} your own assessment. Another approver must.")
            : null;
    }

    private static bool IsAssessor(AssetCriticalityAssessment assessment, CurrentUser? user) =>
        IsSameUser(user, assessment.AssessedByUserId, assessment.AssessedBy)
        || IsSameUser(user, assessment.CreatedByUserId, assessment.CreatedBy)
        || IsSameUser(user, assessment.SubmittedByUserId, assessment.SubmittedBy);

    private static bool IsSameUser(CurrentUser? user, string? userId, string? userName)
    {
        if (user is null)
        {
            return false;
        }

        var mine = user.UserId ?? user.Id;
        if (!string.IsNullOrWhiteSpace(mine) && !string.IsNullOrWhiteSpace(userId))
        {
            return string.Equals(mine, userId, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(userName) && string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase);
    }

    // ----- Response building -----------------------------------------------------------------------------------------

    private static async Task<AssessmentDetailResponse> DetailAsync(AssetCriticalityAssessment a, AssessmentServices s, HttpContext http, CancellationToken cancellationToken)
    {
        var asset = await s.Assets.GetByAssetRegistrationIdAsync(a.AssetRegistrationId, cancellationToken);
        var levels = await s.Levels.GetLevelsAsync(cancellationToken);
        var level = levels.FirstOrDefault(l => string.Equals(l.Name, a.CriticalityLevel, StringComparison.OrdinalIgnoreCase));
        var user = s.Users.GetCurrentUser();
        var status = Status(a);
        var perms = http.Permissions();

        // Rule of the scenario the assessment will (or did) go through: stored once submitted, else worked out now.
        var scenario = a.ApprovalScenario ?? CriticalityScoring.Scenario(a.CriticalityLevel, a.PreviousLevel);
        var rule = await s.ApprovalRules.GetByScenarioAsync(user?.ClientId, scenario, cancellationToken);
        var approval = a.ApprovalScenario is not null && a.ApproverRoles.Count > 0
            ? new AssessmentApprovalInfo(scenario, rule?.Label, a.ApproverRoles, a.ApprovalType, rule?.Description)
            : ToApproval(scenario, rule);

        var editable = Editable.Contains(status) && perms.Contains(AssetCriticalityPermissions.Assess) && CanChangeDraft(a, user, http);
        var pending = Pending.Contains(status);
        var mayDecide = pending && CriticalityAccess.CanApprove(perms, a.CriticalityLevel, a.PreviousLevel);
        var self = pending && IsAssessor(a, user);
        string? blocked = self && mayDecide ? "You assessed this asset, so another approver must decide." : pending && !mayDecide ? "Your role can't approve this level." : null;

        return new AssessmentDetailResponse(
            a.Id,
            a.AssessmentNumber,
            asset is null
                ? new AssessmentAssetInfo(a.AssetRegistrationId, a.AssetName, a.AssetTag, a.Category, null, null, a.Location, a.Department, null)
                : ToAssetInfo(asset),
            a.AssessmentType ?? AssetCriticalityAssessmentTypes.Initial,
            status,
            a.IsCurrent,
            a.AssessmentDate,
            a.AssessedBy,
            a.TemplateId,
            a.TemplateName ?? a.TemplateCode,
            a.TemplateVersion,
            a.Criteria.Select(c => new AssessmentBreakdownRow(c.CriterionId, c.Code ?? c.Name, c.Name, (double)c.Weight, c.Rating ?? (int)Math.Round(c.Score, MidpointRounding.AwayFromZero), c.WeightedScore, ContractText.TrimOrNull(c.Justification))).ToList(),
            a.CriticalityScore,
            a.CalculatedLevel ?? a.CriticalityLevel,
            a.CriticalityLevel,
            a.CriticalityLevelId ?? level?.Id,
            level?.Color,
            a.IsOverride,
            a.Justification,
            a.PreviousLevel,
            a.PreviousApprovedAssessmentId,
            new AssessmentRiskResponse(a.Risk.FailureConsequences, a.Risk.AlternativeEquipmentAvailable, a.Risk.DowntimeImpact, a.Risk.FinancialLoss, a.Risk.SafetyEnvironmentalConsequences),
            a.Attachments.Select(x => new AssessmentAttachmentResponse(x.FileName, x.FileUrl, x.FileType, x.FileSize, x.UploadedBy, x.UploadedAt)).ToList(),
            a.NextReviewDate,
            a.Remarks,
            approval,
            a.RejectedReason,
            a.SubmittedBy,
            a.SubmittedAt,
            string.IsNullOrWhiteSpace(a.ApprovedBy) ? null : a.ApprovedBy,
            a.ApprovedAt,
            a.CreatedBy,
            a.CreatedAt,
            a.Trail.OrderBy(t => t.At).Select(t => new AssessmentTrailResponse(t.Action, t.By, t.At, t.Comments)).ToList(),
            a.ConcurrencyVersion,
            a.TemplateId is null,
            new AssessmentActions(editable, editable, editable, mayDecide && !self, mayDecide && !self, blocked));
    }

    private static string Status(AssetCriticalityAssessment a) => a.ApprovalStatus ?? AssetCriticalityApprovalStatuses.Approved;

    private static AssessmentAssetInfo ToAssetInfo(AssetRegistration asset) => new(
        asset.AssetRegistrationId,
        asset.BasicInformation.AssetName,
        asset.BasicInformation.AssetTag,
        asset.Classification.Category,
        asset.Classification.AssetType,
        asset.LocationAssignment.Location.SiteName,
        asset.LocationAssignment.Location.LocationPath,
        asset.Classification.Department,
        asset.LocationAssignment.Assignment.AssignedToName);

    private static AssessmentLevelResponse ToLevel(CriticalityLevelInfo level) => new(level.Id, level.Name, level.Code, level.Color, level.MinScore, level.MaxScore);

    private static AssessmentBreakdownRow ToBreakdown(CriticalityScoring.ScoredCriterion row) =>
        new(row.Criterion.CriterionId, row.Criterion.Code, row.Criterion.Name, row.Criterion.Weight, row.Rating, row.Weighted, null);

    private static AssessmentApprovalInfo ToApproval(string scenario, AssetCriticalityApprovalRule? rule) =>
        new(scenario, rule?.Label, rule?.ApproverRoles ?? [], rule?.ApprovalType, rule?.Description);

    private static AssessmentTemplateResponse ToTemplate(AssetCriticalityTemplate t, IReadOnlyDictionary<string, string?> descriptions) => new(
        t.Id, t.Code, t.Name, t.Version, t.IsDefault,
        t.Criteria.Select(c => new AssessmentTemplateCriterionResponse(c.CriterionId, c.Code, c.Name, c.Weight, descriptions.GetValueOrDefault(c.Code))).ToList());

    // ----- Small helpers ---------------------------------------------------------------------------------------------

    private static void Trail(AssetCriticalityAssessment assessment, string action, CurrentUser? user, string? comments = null) =>
        assessment.Trail.Add(TrailEntry(action, user, comments));

    private static AssetCriticalityTrailEntry TrailEntry(string action, CurrentUser? user, string? comments) =>
        new() { Action = action, By = user?.UserName, ByUserId = user?.UserId ?? user?.Id, At = DateTime.UtcNow, Comments = comments };

    private static void LimitText(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        if (value?.Trim().Length > max)
        {
            errors[field] = [$"{field} cannot be longer than {max} characters"];
        }
    }

    private static string Describe(AssetCriticalityAssessment a, string what) =>
        $"{a.AssetRegistrationId} - {a.CriticalityLevel} ({a.CriticalityScore}/100), {what}";

    private static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult NotFound(string id) => Results.NotFound(new { message = $"Criticality assessment '{id}' was not found." });
}

/// <summary>The services the assessment endpoints share (one parameter instead of a dozen).</summary>
public sealed record AssessmentServices(
    IAssetCriticalityAssessmentRepository Repository,
    IAssetRegistrationRepository Assets,
    AssetCriticalityTemplateRepository Templates,
    AssetCriticalityApprovalRuleRepository ApprovalRules,
    AssetCriticalityReassessmentRuleRepository ReassessmentRules,
    AssetCriticalityHistoryRepository History,
    ICriticalityLevelClient Levels,
    ICurrentUserAccessor Users,
    IEventLogger Events);
