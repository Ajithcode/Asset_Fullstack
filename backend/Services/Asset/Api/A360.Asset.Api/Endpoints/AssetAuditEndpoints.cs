using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>The Asset Audit list and the New Asset Audit wizard.</summary>
/// <remarks>
/// The audit is created as a Draft on step 1 and each step is saved with its own PUT (add ?saveAsDraft=true for
/// Save as Draft, which skips the required fields). Submit checks every step, fixes the list of assets and moves
/// the audit to Scheduled; Start and Complete follow. Drafts and Scheduled audits can still be edited.
/// When the selection method is not "Selected Assets", the assets in scope are worked out from the asset register.
/// View History reads GET /api/eventlogs?entityType=AssetAudit&amp;entityId={id}.
/// </remarks>
public static class AssetAuditEndpoints
{
    private const string EntityType = "AssetAudit";
    private const string SequencePrefix = "asset-audit-";
    private const string ReferencePrefix = "AUD-";
    private const string CriterionIdPrefix = "crt-";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int BasicInformationStep = 1;
    private const int AssetsStep = 2;
    private const int CriteriaStep = 3;
    private const int ScheduleStep = 4;
    private const int ReviewStep = 5;

    private const int MaxRemarksLength = 1000;

    public static RouteGroupBuilder MapAssetAuditEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-audits").WithTags("Asset Audit");

        // List page.
        group.MapGet("", GetAuditsAsync).WithName("GetAssetAudits");
        group.MapGet("/export", ExportAuditsAsync).WithName("ExportAssetAudits");
        group.MapPost("/bulk-delete", DeleteAuditsAsync).WithName("DeleteAssetAudits");

        // Wizard lookups.
        group.MapGet("/options", GetOptions).WithName("GetAssetAuditOptions");
        group.MapGet("/criteria-templates", () => Results.Ok(AssetAuditCriterionTemplates.All)).WithName("GetAssetAuditCriteriaTemplates");
        group.MapPost("/scope/preview", PreviewScopeAsync).WithName("PreviewAssetAuditScope");

        // Step 1 of 5: Basic Information (creates the draft, then updates it).
        group.MapPost("", CreateAuditAsync).WithName("CreateAssetAudit");
        group.MapPut("/{id}/basic-information", UpdateBasicInformationAsync).WithName("UpdateAssetAuditBasicInformation");

        // Step 2 of 5: Scope & Assets.
        group.MapGet("/{id}/assets", GetAuditAssetsAsync).WithName("GetAssetAuditAssets");
        group.MapPut("/{id}/assets", UpdateAssetsAsync).WithName("UpdateAssetAuditAssets");
        group.MapPost("/{id}/assets/import", ImportAssetsAsync).WithName("ImportAssetAuditAssets").DisableAntiforgery();

        // Step 3 of 5: Audit Criteria.
        group.MapPut("/{id}/criteria", UpdateCriteriaAsync).WithName("UpdateAssetAuditCriteria");

        // Step 4 of 5: Assign & Schedule.
        group.MapPut("/{id}/schedule", UpdateScheduleAsync).WithName("UpdateAssetAuditSchedule");
        group.MapPost("/{id}/schedule/plan", GeneratePlanAsync).WithName("GenerateAssetAuditPlan");

        // Step 5 of 5: Review & Submit.
        group.MapPut("/{id}/review", UpdateReviewAsync).WithName("UpdateAssetAuditReview");
        group.MapPost("/{id}/submit", SubmitAuditAsync).WithName("SubmitAssetAudit");

        // Carrying out the audit.
        group.MapPost("/{id}/start", StartAuditAsync).WithName("StartAssetAudit");
        group.MapPost("/{id}/complete", CompleteAuditAsync).WithName("CompleteAssetAudit");

        group.MapGet("/{id}", GetAuditAsync).WithName("GetAssetAudit");
        group.MapDelete("/{id}", DeleteAuditAsync).WithName("DeleteAssetAudit");

        return group;
    }

    private static IResult GetOptions() => Results.Ok(new AssetAuditOptionsResponse(
        AssetAuditOptions.AuditTypes.All,
        AssetAuditOptions.ListStatuses.All,
        AssetAuditOptions.SelectionMethods.All,
        AssetAuditCriteriaOptions.Labels,
        AssetAuditOptions.CriterionTypes.All,
        AssetAuditOptions.CheckMethods.All,
        AssetAuditOptions.AppliesTo.All,
        AssetAuditOptions.Roles.All,
        AssetAuditOptions.WorkingDays.All,
        AssetAuditOptions.DaysOfWeek.All,
        AssetAuditOptions.LocationSequences.All));

    /// <summary>The Asset Audit list with its summary cards. Status is Draft, Scheduled, In Progress, Completed or Overdue; empty lists all but drafts.</summary>
    private static async Task<IResult> GetAuditsAsync(
        IAssetAuditRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? auditType = null,
        string? status = null,
        string? locationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetAuditRepository.DefaultPageSize)
    {
        if (StatusProblem(status) is { } problem)
        {
            return problem;
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetAuditRepository.MaxPageSize ? AssetAuditRepository.DefaultPageSize : pageSize;

        var today = Today();
        var result = await repository.QueryAsync(
            new AssetAuditQuery(search, auditType, status, locationId, ContractText.DateOnly(fromDate), ContractText.DateOnly(toDate), today, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetAuditListResponse(
            result.Items.Select(audit => AssetAuditListItemResponse.FromEntity(audit, today)).ToList(),
            result.TotalCount,
            AssetAuditCounts.FromStatusCounts(result.StatusCounts),
            page,
            pageSize));
    }

    /// <summary>Export: the list, with the same filters, as an Excel file.</summary>
    private static async Task<IResult> ExportAuditsAsync(
        IAssetAuditRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? auditType = null,
        string? status = null,
        string? locationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        if (StatusProblem(status) is { } problem)
        {
            return problem;
        }

        var today = Today();
        var result = await repository.QueryAsync(
            new AssetAuditQuery(search, auditType, status, locationId, ContractText.DateOnly(fromDate), ContractText.DateOnly(toDate), today, 1, 0),
            cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Asset Audits");
        string[] headers = ["Audit Reference", "Audit Name", "Audit Type", "Location / Site", "Scheduled Date", "End Date", "Completed Date", "Status", "Audited By", "Assets", "Discrepancies"];
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in result.Items.Select(audit => AssetAuditListItemResponse.FromEntity(audit, today)))
        {
            sheet.Cell(row, 1).Value = item.AuditReference;
            sheet.Cell(row, 2).Value = item.AuditName;
            sheet.Cell(row, 3).Value = item.AuditType;
            sheet.Cell(row, 4).Value = item.Location;
            sheet.Cell(row, 5).Value = item.ScheduledDate;
            sheet.Cell(row, 6).Value = item.EndDate;
            sheet.Cell(row, 7).Value = item.CompletedDate;
            sheet.Cell(row, 8).Value = item.Status;
            sheet.Cell(row, 9).Value = item.AuditedBy;
            sheet.Cell(row, 10).Value = item.AssetCount;
            sheet.Cell(row, 11).Value = item.DiscrepancyCount;
            row++;
        }

        sheet.Range(2, 5, Math.Max(2, row - 1), 7).Style.DateFormat.Format = "dd MMM yyyy";
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Results.File(stream.ToArray(), ExcelContentType, $"asset-audits-{today:yyyyMMdd}.xlsx");
    }

    private static async Task<IResult> GetAuditAsync(
        string id,
        IAssetAuditRepository repository,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        return audit is null ? AuditNotFound(id) : Results.Ok(AssetAuditResponse.FromEntity(audit, Today()));
    }

    /// <summary>Preview Assets on step 1, before the audit is saved: the assets the scope covers.</summary>
    private static async Task<IResult> PreviewScopeAsync(
        AssetAuditScopeRequest request,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = AssetAuditRepository.DefaultPageSize)
    {
        var scope = request.ToEntity();
        if (scope.SelectionMethod is not null && !AssetAuditOptions.SelectionMethods.IsValid(scope.SelectionMethod))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["SelectionMethod"] = [$"SelectionMethod must be one of: {string.Join(", ", AssetAuditOptions.SelectionMethods.All)}"]
            });
        }

        var found = await assetRepository.FindInScopeAsync(ScopeQuery(scope), AssetAuditValidationExtensions.MaxAssets + 1, cancellationToken);
        return Results.Ok(PageAssets(found.Take(AssetAuditValidationExtensions.MaxAssets).Select(asset => ToAuditAsset(asset, [])).ToList(),
            found.Count > AssetAuditValidationExtensions.MaxAssets, search, page, pageSize));
    }

    /// <summary>Step 1 Next on a new audit (or Save as Draft with ?saveAsDraft=true).</summary>
    private static async Task<IResult> CreateAuditAsync(
        AssetAuditBasicInformationRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        var audit = new AssetAudit
        {
            Status = AssetAuditRepository.DraftStatus,
            CurrentStep = BasicInformationStep,
            CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        var error = await ApplyBasicInformationAsync(audit, request, saveAsDraft ?? false, repository, assetRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (string.IsNullOrEmpty(audit.AuditReference))
        {
            // e.g. "AUD-2025-025": numbered per year.
            var year = DateTime.UtcNow.Year;
            var nextSequence = await sequenceGenerator.GetNextValueAsync($"{SequencePrefix}{year}", cancellationToken);
            audit.AuditReference = $"{ReferencePrefix}{year}-{nextSequence:D3}";
        }

        await repository.CreateAsync(audit, cancellationToken);
        await eventLogger.LogAsync(EntityType, audit.Id, Describe(audit), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-audits/{audit.Id}", AssetAuditResponse.FromEntity(audit, Today()));
    }

    private static async Task<IResult> UpdateBasicInformationAsync(
        string id,
        AssetAuditBasicInformationRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        var error = await ApplyBasicInformationAsync(audit, request, IsDraftSave(audit, saveAsDraft), repository, assetRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (string.IsNullOrEmpty(audit.AuditReference))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AuditReference"] = ["AuditReference cannot be removed once the audit is created"]
            });
        }

        return await SaveStepAsync(audit, BasicInformationStep, "basic information", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Copies step 1 onto the audit and checks it. Until step 3 is saved, the criteria follow the ticked
    /// Audit Criteria checkboxes, and Photo capture turns on Capture photos.
    /// </summary>
    private static async Task<IResult?> ApplyBasicInformationAsync(
        AssetAudit audit,
        AssetAuditBasicInformationRequest request,
        bool isDraft,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var options = (request.CriteriaOptions ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => AssetAuditCriteriaOptions.Normalize(option) ?? option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        audit.AuditName = request.AuditName?.Trim() ?? string.Empty;
        audit.AuditType = AssetAuditOptions.AuditTypes.NormalizeOrKeep(request.AuditType);
        audit.Description = ContractText.TrimOrNull(request.Description);
        audit.Scope = (request.Scope ?? new AssetAuditScopeRequest(null, null, null, null, null, null, null, null, null, null, null, null)).ToEntity();
        audit.CriteriaOptions = options;
        audit.OtherCriteriaDetails = options.Contains(AssetAuditCriteriaOptions.Other) ? ContractText.TrimOrNull(request.OtherCriteriaDetails) : null;

        // An existing audit keeps its reference when none is sent.
        if (ContractText.TrimOrNull(request.AuditReference) is { } reference)
        {
            audit.AuditReference = reference;
        }

        var errors = new Dictionary<string, string[]>();
        audit.ValidateBasicInformation(errors, isDraft);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!string.IsNullOrEmpty(audit.AuditReference)
            && await repository.ReferenceExistsAsync(audit.AuditReference, audit.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Audit reference '{audit.AuditReference}' is already used." });
        }

        var inScope = await assetRepository.FindInScopeAsync(ScopeQuery(audit.Scope), AssetAuditValidationExtensions.MaxAssets + 1, cancellationToken);
        audit.Scope.TotalAssetsInScope = inScope.Count;

        if (audit.CurrentStep < CriteriaStep)
        {
            audit.Criteria = CriteriaFromOptions(audit);
            audit.CriteriaSettings.CapturePhotos = options.Contains(AssetAuditCriteriaOptions.PhotoCapture);
        }

        return null;
    }

    /// <summary>The assets the audit covers (the scope, or the picked assets for "Selected Assets"), for the Preview Assets list.</summary>
    private static async Task<IResult> GetAuditAssetsAsync(
        string id,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = AssetAuditRepository.DefaultPageSize)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        var (assets, isTruncated) = audit.Status == AssetAuditRepository.DraftStatus
            ? await ResolveAssetsAsync(audit, assetRepository, cancellationToken)
            : (audit.Assets, false);

        return Results.Ok(PageAssets(assets, isTruncated, search, page, pageSize));
    }

    /// <summary>Step 2 Next: the ticked assets and their sensors / components. The list replaces the saved one.</summary>
    private static async Task<IResult> UpdateAssetsAsync(
        string id,
        AssetAuditAssetsRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        var selections = (request.Assets ?? [])
            .Where(selection => !string.IsNullOrWhiteSpace(selection.AssetRegistrationId))
            .GroupBy(selection => selection.AssetRegistrationId!.Trim(), StringComparer.Ordinal)
            .Select(group => (Id: group.Key, ComponentIds: ContractText.CleanList(group.SelectMany(selection => selection.ComponentIds ?? []), StringComparer.Ordinal)))
            .ToList();

        if (selections.Count > AssetAuditValidationExtensions.MaxAssets)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Assets"] = [$"At most {AssetAuditValidationExtensions.MaxAssets} assets can be audited at once"]
            });
        }

        var errors = new Dictionary<string, string[]>();
        var found = await assetRepository.FindInScopeAsync(
            new AssetScopeQuery(null, null, null, null, null, null, null, null, selections.Select(selection => selection.Id).ToList()),
            selections.Count + 1,
            cancellationToken);
        var assetsById = found.ToDictionary(asset => asset.AssetRegistrationId, StringComparer.Ordinal);

        var assets = new List<AssetAuditAsset>();
        for (var index = 0; index < selections.Count; index++)
        {
            var (assetId, componentIds) = selections[index];
            if (!assetsById.TryGetValue(assetId, out var asset))
            {
                errors[$"Assets[{index}].AssetRegistrationId"] = [$"Asset '{assetId}' was not found"];
                continue;
            }

            if (componentIds.Count > 0)
            {
                var components = (await componentRepository.GetByAssetAsync(assetId, cancellationToken))
                    .Select(component => component.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (componentIds.FirstOrDefault(componentId => !components.Contains(componentId)) is { } unknown)
                {
                    errors[$"Assets[{index}].ComponentIds"] = [$"Component '{unknown}' does not belong to asset '{assetId}'"];
                    continue;
                }
            }

            assets.Add(ToAuditAsset(asset, componentIds));
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        audit.Assets = assets;
        audit.ValidateAssets(errors, IsDraftSave(audit, saveAsDraft));
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return await SaveStepAsync(audit, AssetsStep, $"{assets.Count} assets selected", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Import from Excel on step 2: upload an .xlsx or .csv as form field "file" with an "Asset Tag" (or serial number)
    /// column; without one, the first column is read. Found assets are added to the selection.
    /// </summary>
    private static async Task<IResult> ImportAssetsAsync(
        string id,
        IFormFile? file,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        var (tags, fileError) = await AssetTagFileReader.ReadAsync(file, cancellationToken);
        if (fileError is not null)
        {
            return FileProblem(fileError);
        }

        if (tags.Count > AssetAuditValidationExtensions.MaxAssets)
        {
            return FileProblem($"At most {AssetAuditValidationExtensions.MaxAssets} assets can be audited at once");
        }

        var found = await assetRepository.FindByTagsOrSerialsAsync(tags, cancellationToken);
        var matched = new HashSet<string>(StringComparer.Ordinal);
        var existing = audit.Assets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.Ordinal);
        var imported = 0;

        foreach (var asset in found)
        {
            matched.Add(asset.BasicInformation.AssetTag ?? string.Empty);
            matched.Add(asset.BasicInformation.SerialNumber ?? string.Empty);
            if (existing.Add(asset.AssetRegistrationId))
            {
                audit.Assets.Add(ToAuditAsset(asset, []));
                imported++;
            }
        }

        var errors = new Dictionary<string, string[]>();
        audit.ValidateAssets(errors, saveAsDraft: true);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        audit.CurrentStep = Math.Max(audit.CurrentStep, AssetsStep);
        if (!await SaveAsync(audit, repository, currentUserAccessor.GetCurrentUser()?.UserName, cancellationToken))
        {
            return AuditNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, audit.Id, $"{Describe(audit)} - {imported} assets imported from {file!.FileName}", EventAction.Updated, cancellationToken);
        return Results.Ok(new AssetAuditImportResponse(
            AssetAuditResponse.FromEntity(audit, Today()),
            imported,
            tags.Where(tag => !matched.Contains(tag)).ToList()));
    }

    /// <summary>Step 3 Next: every criterion across the tabs, and the Criteria Settings. The list replaces the saved one.</summary>
    private static async Task<IResult> UpdateCriteriaAsync(
        string id,
        AssetAuditCriteriaRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAuditRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        var settings = request.Settings?.ToEntity() ?? audit.CriteriaSettings;
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var nextNumber = NextCriterionNumber(audit.Criteria.Select(criterion => criterion.CriterionId)
            .Concat((request.Criteria ?? []).Select(criterion => criterion.CriterionId ?? string.Empty)));

        var criteria = new List<AssetAuditCriterion>();
        foreach (var item in request.Criteria ?? [])
        {
            // A missing or repeated id gets a new one.
            var criterionId = ContractText.TrimOrNull(item.CriterionId) is { } kept && usedIds.Add(kept)
                ? kept
                : $"{CriterionIdPrefix}{nextNumber++}";
            usedIds.Add(criterionId);

            var appliesTo = AssetAuditOptions.AppliesTo.NormalizeOrKeep(item.AppliesTo) ?? AssetAuditOptions.AllAssets;
            criteria.Add(new AssetAuditCriterion
            {
                CriterionId = criterionId,
                Name = item.Name?.Trim() ?? string.Empty,
                Description = ContractText.TrimOrNull(item.Description),
                Category = ContractText.TrimOrNull(item.Category),
                Type = AssetAuditOptions.CriterionTypes.NormalizeOrKeep(item.Type) ?? string.Empty,
                CheckMethod = AssetAuditOptions.CheckMethods.NormalizeOrKeep(item.CheckMethod),
                AppliesTo = appliesTo,
                AssetRegistrationIds = appliesTo == AssetAuditOptions.SelectedAssets
                    ? ContractText.CleanList(item.AssetRegistrationIds, StringComparer.Ordinal)
                    : [],
                IsMandatory = item.IsMandatory ?? settings.DefaultMandatory
            });
        }

        audit.Criteria = criteria;
        audit.CriteriaSettings = settings;

        var errors = new Dictionary<string, string[]>();
        audit.ValidateCriteria(errors, IsDraftSave(audit, saveAsDraft));
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return await SaveStepAsync(audit, CriteriaStep, $"{criteria.Count} criteria", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Step 4 Next: team, schedule, plan and notifications. With Schedule.AutoPlan and no plan sent, the plan is
    /// generated; a plan that is sent is kept as edited. Each member's Assigned Assets is counted from the plan.
    /// </summary>
    private static async Task<IResult> UpdateScheduleAsync(
        string id,
        AssetAuditScheduleRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        ApplySchedule(audit, request);

        if ((request.Plan ?? []).Count == 0 && audit.Schedule.AutoPlan)
        {
            var (assets, _) = await ResolveAssetsAsync(audit, assetRepository, cancellationToken);
            audit.Plan = AssetAuditPlanner.Generate(assets, audit.Team, audit.Schedule);
        }

        AssetAuditPlanner.CountAssignedAssets(audit.Team, audit.Plan);

        var errors = new Dictionary<string, string[]>();
        audit.ValidateSchedule(errors, IsDraftSave(audit, saveAsDraft));
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return await SaveStepAsync(audit, ScheduleStep, $"scheduled {audit.Schedule.StartDate:dd MMM yyyy} - {audit.Schedule.EndDate:dd MMM yyyy}", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>Regenerate Plan: builds the plan from the team and schedule on screen (Plan is ignored). Nothing is saved.</summary>
    private static async Task<IResult> GeneratePlanAsync(
        string id,
        AssetAuditScheduleRequest request,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        ApplySchedule(audit, request with { Plan = null });

        var errors = new Dictionary<string, string[]>();
        audit.ValidateSchedule(errors, saveAsDraft: true);
        if (audit.Schedule.StartDate is null || audit.Schedule.EndDate is null)
        {
            errors["Schedule.StartDate"] = ["Enter StartDate and EndDate to plan the audit"];
        }

        if (audit.Team.Count == 0)
        {
            errors["Team"] = ["Add at least one team member to plan the audit"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var (assets, _) = await ResolveAssetsAsync(audit, assetRepository, cancellationToken);
        var plan = AssetAuditPlanner.Generate(assets, audit.Team, audit.Schedule);
        AssetAuditPlanner.CountAssignedAssets(audit.Team, plan);

        return Results.Ok(new AssetAuditPlanResponse(
            plan.Select(AssetAuditPlanEntryResponse.FromEntity).ToList(),
            audit.Team,
            AssetAuditPlanner.WorkingDates(audit.Schedule).Count,
            assets.Count));
    }

    /// <summary>Step 5: Attachments and Notes &amp; Instructions (Submit is a separate call).</summary>
    private static async Task<IResult> UpdateReviewAsync(
        string id,
        AssetAuditReviewRequest request,
        IAssetAuditRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (audit, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return problem!;
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        audit.Attachments = (request.Attachments ?? []).Select(attachment => attachment.ToEntity(userName)).ToList();
        audit.Notes = ContractText.TrimOrNull(request.Notes);

        var errors = new Dictionary<string, string[]>();
        audit.ValidateReview(errors);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return await SaveStepAsync(audit, ReviewStep, "attachments and notes", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Submit Audit: fixes the list of assets (the scope, plus picked assets and their components), regenerates an
    /// automatic plan for them, checks every step and moves the audit to Scheduled.
    /// </summary>
    private static async Task<IResult> SubmitAuditAsync(
        string id,
        IAssetAuditRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        if (audit.Status != AssetAuditRepository.DraftStatus)
        {
            return Results.Conflict(new { message = $"Audit {audit.AuditReference} is already submitted." });
        }

        var (assets, isTruncated) = await ResolveAssetsAsync(audit, assetRepository, cancellationToken);
        if (isTruncated)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Assets.Assets"] = [$"More than {AssetAuditValidationExtensions.MaxAssets} assets are in scope; narrow the scope or split the audit"]
            });
        }

        audit.Assets = assets;
        if (audit.Scope.SelectionMethod != AssetAuditOptions.SelectedAssets)
        {
            audit.Scope.TotalAssetsInScope = assets.Count;
        }

        if (audit.Schedule.AutoPlan)
        {
            audit.Plan = AssetAuditPlanner.Generate(assets, audit.Team, audit.Schedule);
        }

        AssetAuditPlanner.CountAssignedAssets(audit.Team, audit.Plan);

        var errors = audit.ValidateForSubmit();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        audit.Status = AssetAuditRepository.ScheduledStatus;
        audit.CurrentStep = ReviewStep;
        audit.SubmittedBy = userName;
        audit.SubmittedAt = DateTime.UtcNow;

        if (!await SaveAsync(audit, repository, userName, cancellationToken))
        {
            return AuditNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, audit.Id, $"{Describe(audit)} submitted with {assets.Count} assets", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetAuditResponse.FromEntity(audit, Today()));
    }

    private static async Task<IResult> StartAuditAsync(
        string id,
        IAssetAuditRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        if (audit.Status != AssetAuditRepository.ScheduledStatus)
        {
            return Results.Conflict(new { message = $"Only a scheduled audit can be started; {audit.AuditReference} is {audit.Status}." });
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        audit.Status = AssetAuditRepository.InProgressStatus;
        audit.StartedAt = DateTime.UtcNow;

        if (!await SaveAsync(audit, repository, userName, cancellationToken))
        {
            return AuditNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, audit.Id, $"{Describe(audit)} started", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetAuditResponse.FromEntity(audit, Today()));
    }

    /// <summary>Completes a scheduled or in-progress audit, recording the number of discrepancies found.</summary>
    private static async Task<IResult> CompleteAuditAsync(
        string id,
        CompleteAssetAuditRequest? request,
        IAssetAuditRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        if (audit.Status is not (AssetAuditRepository.ScheduledStatus or AssetAuditRepository.InProgressStatus))
        {
            return Results.Conflict(new { message = $"Only a scheduled or in-progress audit can be completed; {audit.AuditReference} is {audit.Status}." });
        }

        var errors = new Dictionary<string, string[]>();
        if (request?.DiscrepancyCount < 0)
        {
            errors["DiscrepancyCount"] = ["DiscrepancyCount cannot be negative"];
        }

        if (request?.Remarks?.Trim().Length > MaxRemarksLength)
        {
            errors["Remarks"] = [$"Remarks cannot be longer than {MaxRemarksLength} characters"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        audit.Status = AssetAuditRepository.CompletedStatus;
        audit.StartedAt ??= DateTime.UtcNow;
        audit.CompletedAt = DateTime.UtcNow;
        audit.DiscrepancyCount = request?.DiscrepancyCount ?? 0;
        audit.CompletionRemarks = ContractText.TrimOrNull(request?.Remarks);
        foreach (var entry in audit.Plan)
        {
            entry.Status = AssetAuditRepository.CompletedStatus;
        }

        if (!await SaveAsync(audit, repository, userName, cancellationToken))
        {
            return AuditNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, audit.Id, $"{Describe(audit)} completed with {audit.DiscrepancyCount} discrepancies", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetAuditResponse.FromEntity(audit, Today()));
    }

    /// <summary>Deletes a draft or scheduled audit; one that has started is kept as a record.</summary>
    private static async Task<IResult> DeleteAuditAsync(
        string id,
        IAssetAuditRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return AuditNotFound(id);
        }

        if (!IsEditable(audit))
        {
            return NotEditable(audit);
        }

        return await DeleteOneAsync(audit, repository, eventLogger, cancellationToken)
            ? Results.NoContent()
            : AuditNotFound(id);
    }

    /// <summary>Deletes the ticked audits. Ids that are not found, and audits that have started, are skipped.</summary>
    private static async Task<IResult> DeleteAuditsAsync(
        DeleteAssetAuditsRequest request,
        IAssetAuditRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(request.Ids, StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Ids"] = ["Select at least one audit"]
            });
        }

        var deleted = 0;
        var skipped = new List<string>();
        foreach (var id in ids)
        {
            var audit = await FindAsync(id, repository, cancellationToken);
            if (audit is null)
            {
                continue;
            }

            if (!IsEditable(audit))
            {
                skipped.Add(audit.AuditReference);
            }
            else if (await DeleteOneAsync(audit, repository, eventLogger, cancellationToken))
            {
                deleted++;
            }
        }

        return Results.Ok(new { deletedCount = deleted, skipped });
    }

    private static async Task<bool> DeleteOneAsync(AssetAudit audit, IAssetAuditRepository repository, IEventLogger eventLogger, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(audit.Id, cancellationToken))
        {
            return false;
        }

        await eventLogger.LogAsync(EntityType, audit.Id, Describe(audit), EventAction.Deleted, cancellationToken);
        return true;
    }

    private static void ApplySchedule(AssetAudit audit, AssetAuditScheduleRequest request)
    {
        audit.Team = (request.Team ?? []).Select(member => member.ToEntity()).ToList();
        audit.Schedule = request.Schedule?.ToEntity() ?? new AssetAuditSchedule();
        audit.Notifications = request.Notifications?.ToEntity() ?? new AssetAuditNotifications();

        var names = audit.Team
            .Where(member => member.UserId.Length > 0)
            .GroupBy(member => member.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        audit.Plan = (request.Plan ?? [])
            .Select(entry =>
            {
                var assignedTo = ContractText.TrimOrNull(entry.AssignedToUserId);
                return new AssetAuditPlanEntry
                {
                    Date = ContractText.DateOnly(entry.Date) ?? default,
                    Location = ContractText.TrimOrNull(entry.Location),
                    AssetRegistrationIds = ContractText.CleanList(entry.AssetRegistrationIds, StringComparer.Ordinal),
                    AssignedToUserId = assignedTo,
                    AssignedToName = assignedTo is null ? null : names.GetValueOrDefault(assignedTo),
                    Status = AssetAuditOptions.PlanStatuses.NormalizeOrKeep(entry.Status) ?? AssetAuditRepository.ScheduledStatus
                };
            })
            .OrderBy(entry => entry.Date)
            .ToList();
    }

    /// <summary>
    /// The assets the audit covers. For "Selected Assets" these are the picked assets; otherwise every asset in
    /// scope, keeping the components picked for any of them, plus picked assets outside the scope.
    /// </summary>
    private static async Task<(List<AssetAuditAsset> Assets, bool IsTruncated)> ResolveAssetsAsync(
        AssetAudit audit,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        if (audit.Scope.SelectionMethod is null or AssetAuditOptions.SelectedAssets)
        {
            return (audit.Assets.ToList(), false);
        }

        var found = await assetRepository.FindInScopeAsync(ScopeQuery(audit.Scope), AssetAuditValidationExtensions.MaxAssets + 1, cancellationToken);
        var picked = audit.Assets.ToDictionary(asset => asset.AssetRegistrationId, StringComparer.Ordinal);

        var assets = found
            .Take(AssetAuditValidationExtensions.MaxAssets)
            .Select(asset => ToAuditAsset(asset, picked.TryGetValue(asset.AssetRegistrationId, out var selection) ? selection.ComponentIds : []))
            .ToList();

        var inScope = assets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.Ordinal);
        assets.AddRange(audit.Assets.Where(asset => !inScope.Contains(asset.AssetRegistrationId)));

        return (assets, found.Count > AssetAuditValidationExtensions.MaxAssets || assets.Count > AssetAuditValidationExtensions.MaxAssets);
    }

    private static AssetScopeQuery ScopeQuery(AssetAuditScope scope) => new(
        scope.LocationId,
        scope.DepartmentName,
        scope.CategoryName,
        scope.TagFrom,
        scope.TagTo,
        scope.AssetStatus,
        scope.AssetType,
        scope.Criticality);

    private static AssetAuditAsset ToAuditAsset(AssetRegistration asset, List<string> componentIds) => new()
    {
        AssetRegistrationId = asset.AssetRegistrationId,
        AssetName = asset.BasicInformation.AssetName,
        AssetTag = asset.BasicInformation.AssetTag,
        Category = asset.Classification.Category,
        AssetType = asset.Classification.AssetType,
        Location = asset.LocationAssignment.Location.LocationPath,
        Status = asset.BasicInformation.Status,
        ComponentIds = componentIds
    };

    private static AssetAuditAssetsResponse PageAssets(List<AssetAuditAsset> assets, bool isTruncated, string? search, int page, int pageSize)
    {
        var filtered = string.IsNullOrWhiteSpace(search)
            ? assets
            : assets.Where(asset => new[] { asset.AssetName, asset.AssetTag, asset.Category, asset.AssetType, asset.Location }
                .Any(value => value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)).ToList();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetAuditRepository.MaxPageSize ? AssetAuditRepository.DefaultPageSize : pageSize;

        return new AssetAuditAssetsResponse(
            filtered.Count,
            isTruncated,
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize);
    }

    /// <summary>The built-in checklist criteria for the ticked step 1 checkboxes, plus one custom criterion for "Other".</summary>
    private static List<AssetAuditCriterion> CriteriaFromOptions(AssetAudit audit)
    {
        var number = 1;
        var criteria = AssetAuditCriterionTemplates.All
            .Where(template => template.OptionKey is not null && audit.CriteriaOptions.Contains(template.OptionKey))
            .Select(template => template.ToCriterion($"{CriterionIdPrefix}{number++}"))
            .ToList();

        if (audit.OtherCriteriaDetails is { } other)
        {
            criteria.Add(new AssetAuditCriterion
            {
                CriterionId = $"{CriterionIdPrefix}{number}",
                Name = other,
                Category = "Custom",
                Type = "Custom",
                AppliesTo = AssetAuditOptions.AllAssets,
                IsMandatory = false
            });
        }

        return criteria;
    }

    private static int NextCriterionNumber(IEnumerable<string> criterionIds) =>
        criterionIds
            .Where(criterionId => criterionId.StartsWith(CriterionIdPrefix, StringComparison.Ordinal))
            .Select(criterionId => int.TryParse(criterionId[CriterionIdPrefix.Length..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

    private static async Task<IResult> SaveStepAsync(
        AssetAudit audit,
        int step,
        string change,
        IAssetAuditRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        audit.CurrentStep = Math.Max(audit.CurrentStep, step);
        if (!await SaveAsync(audit, repository, currentUserAccessor.GetCurrentUser()?.UserName, cancellationToken))
        {
            return AuditNotFound(audit.Id);
        }

        await eventLogger.LogAsync(EntityType, audit.Id, $"{Describe(audit)} - {change}", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetAuditResponse.FromEntity(audit, Today()));
    }

    private static async Task<bool> SaveAsync(AssetAudit audit, IAssetAuditRepository repository, string? userName, CancellationToken cancellationToken)
    {
        audit.UpdatedBy = userName;
        audit.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(audit.Id, audit, cancellationToken);
    }

    private static async Task<AssetAudit?> FindAsync(string id, IAssetAuditRepository repository, CancellationToken cancellationToken)
    {
        var audit = await repository.GetByIdAsync(id, cancellationToken);
        return audit is null || audit.IsDeleted ? null : audit;
    }

    private static async Task<(AssetAudit? Audit, IResult? Problem)> FindEditableAsync(string id, IAssetAuditRepository repository, CancellationToken cancellationToken)
    {
        var audit = await FindAsync(id, repository, cancellationToken);
        if (audit is null)
        {
            return (null, AuditNotFound(id));
        }

        return IsEditable(audit) ? (audit, null) : (null, NotEditable(audit));
    }

    /// <summary>Drafts and scheduled audits can be changed; once started, an audit is a record.</summary>
    private static bool IsEditable(AssetAudit audit) =>
        audit.Status is AssetAuditRepository.DraftStatus or AssetAuditRepository.ScheduledStatus;

    /// <summary>Save as Draft only relaxes the rules while the audit is a draft; a scheduled audit must stay complete.</summary>
    private static bool IsDraftSave(AssetAudit audit, bool? saveAsDraft) =>
        saveAsDraft == true && audit.Status == AssetAuditRepository.DraftStatus;

    private static IResult? StatusProblem(string? status) =>
        string.IsNullOrWhiteSpace(status) || AssetAuditOptions.ListStatuses.IsValid(status)
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Status"] = [$"Status must be one of: {string.Join(", ", AssetAuditOptions.ListStatuses.All)}"]
            });

    private static DateTime Today() => DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    private static string Describe(AssetAudit audit) => $"{audit.AuditReference} - {audit.AuditName}";

    private static IResult NotEditable(AssetAudit audit) =>
        Results.Conflict(new { message = $"Audit {audit.AuditReference} is {audit.Status} and can no longer be changed." });

    private static IResult FileProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["File"] = [message]
        });

    private static IResult AuditNotFound(string id) =>
        Results.NotFound(new { message = $"Asset audit '{id}' was not found." });
}
