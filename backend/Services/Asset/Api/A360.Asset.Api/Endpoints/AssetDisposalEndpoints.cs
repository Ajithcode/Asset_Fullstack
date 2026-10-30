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

/// <summary>Disposal requests and the New Disposal Request wizard.</summary>
/// <remarks>
/// The request is created as a Draft on step 1 and each step is saved with its own PUT (add ?saveAsDraft=true for
/// Save as Draft, which skips the required fields). Submit checks every step and sends the request for approval.
/// Levels are approved in order; when every Mandatory level has approved, the request is Approved, and Complete
/// records the actual disposal and marks the assets Disposed. A rejected request can be edited and submitted again.
/// Step 2's asset list is GET /api/asset-registrations (with category, locationId, condition and status filters).
/// View History reads GET /api/eventlogs?entityType=AssetDisposalRequest&amp;entityId={id}.
/// </remarks>
public static class AssetDisposalEndpoints
{
    private const string EntityType = "AssetDisposalRequest";
    private const string AssetEntityType = "AssetRegistration";
    private const string SequenceName = "asset-disposal-request";
    private const string RequestNumberPrefix = "DSP-";

    private const int DetailsStep = 1;
    private const int AssetsStep = 2;
    private const int MethodStep = 3;
    private const int ApprovalStep = 4;
    private const int ReviewStep = 5;

    private const int CommentsMaxLength = 500;

    public static RouteGroupBuilder MapAssetDisposalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-disposals").WithTags("Asset Disposal");

        group.MapGet("", GetRequestsAsync).WithName("GetAssetDisposalRequests");
        group.MapGet("/options", GetOptions).WithName("GetAssetDisposalOptions");

        // Step 1 of 5: Request Details (creates the draft, then updates it).
        group.MapPost("", CreateRequestAsync).WithName("CreateAssetDisposalRequest");
        group.MapPut("/{id}/details", UpdateDetailsAsync).WithName("UpdateAssetDisposalDetails");

        // Step 2 of 5: Select Assets.
        group.MapPut("/{id}/assets", UpdateAssetsAsync).WithName("UpdateAssetDisposalAssets");
        group.MapPost("/{id}/assets/by-tag", AddAssetsByTagAsync).WithName("AddAssetDisposalAssetsByTag");
        group.MapPost("/{id}/assets/import", ImportAssetsAsync).WithName("ImportAssetDisposalAssets").DisableAntiforgery();

        // Step 3 of 5: Disposal Method.
        group.MapPut("/{id}/method", UpdateMethodAsync).WithName("UpdateAssetDisposalMethod");

        // Step 4 of 5: Approval & Attachments.
        group.MapPut("/{id}/approval", UpdateApprovalAsync).WithName("UpdateAssetDisposalApproval");

        // Step 5 of 5: Review & Submit.
        group.MapPost("/{id}/submit", SubmitRequestAsync).WithName("SubmitAssetDisposalRequest");

        // Approval and after.
        group.MapPost("/{id}/approve", ApproveAsync).WithName("ApproveAssetDisposalRequest");
        group.MapPost("/{id}/reject", RejectAsync).WithName("RejectAssetDisposalRequest");
        group.MapPost("/{id}/skip", SkipAsync).WithName("SkipAssetDisposalApprovalLevel");
        group.MapPost("/{id}/complete", CompleteAsync).WithName("CompleteAssetDisposalRequest");
        group.MapPost("/{id}/cancel", CancelAsync).WithName("CancelAssetDisposalRequest");

        group.MapGet("/{id}", GetRequestAsync).WithName("GetAssetDisposalRequest");
        group.MapDelete("/{id}", DeleteRequestAsync).WithName("DeleteAssetDisposalRequest");

        return group;
    }

    private static IResult GetOptions() => Results.Ok(new AssetDisposalOptionsResponse(
        AssetDisposalOptions.Reasons.All,
        AssetDisposalOptions.Priorities.All,
        AssetDisposalOptions.Methods,
        AssetDisposalOptions.ApprovalTypes.All,
        AssetDisposalOptions.ApprovalLevels,
        AssetDisposalOptions.Statuses.All,
        AssetDisposalOptions.DefaultCurrency));

    /// <summary>
    /// The disposal request list. approverUserId lists Pending Approval requests with a level that user has not yet
    /// acted on ("My approvals").
    /// </summary>
    private static async Task<IResult> GetRequestsAsync(
        IAssetDisposalRequestRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? method = null,
        string? reason = null,
        string? approverUserId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetDisposalRequestRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetDisposalRequestRepository.MaxPageSize ? AssetDisposalRequestRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetDisposalRequestQuery(search, status, method, reason, approverUserId, ContractText.DateOnly(fromDate), ContractText.DateOnly(toDate), page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetDisposalRequestListResponse(
            result.Items.Select(AssetDisposalRequestListItemResponse.FromEntity).ToList(),
            result.TotalCount,
            result.StatusCounts,
            page,
            pageSize));
    }

    private static async Task<IResult> GetRequestAsync(
        string id,
        IAssetDisposalRequestRepository repository,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        return request is null ? RequestNotFound(id) : Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    /// <summary>Step 1 Next on a new request (or Save as Draft with ?saveAsDraft=true). Requested By defaults to the signed-in user.</summary>
    private static async Task<IResult> CreateRequestAsync(
        AssetDisposalDetailsRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetDisposalRequestRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        var request = new AssetDisposalRequest
        {
            Status = AssetDisposalRequestRepository.DraftStatus,
            CurrentStep = DetailsStep,
            CreatedBy = ContractText.FirstNonEmpty(body.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(body.ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(body.TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        ApplyDetails(request, body, currentUser);

        var errors = new Dictionary<string, string[]>();
        request.ValidateDetails(errors, saveAsDraft ?? false);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        request.RequestNumber = $"{RequestNumberPrefix}{nextSequence:D6}";

        await repository.CreateAsync(request, cancellationToken);
        await eventLogger.LogAsync(EntityType, request.Id, Describe(request), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-disposals/{request.Id}", AssetDisposalRequestResponse.FromEntity(request));
    }

    private static async Task<IResult> UpdateDetailsAsync(
        string id,
        AssetDisposalDetailsRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        ApplyDetails(request, body, currentUserAccessor.GetCurrentUser());

        var errors = new Dictionary<string, string[]>();
        request.ValidateDetails(errors, IsDraftSave(request, saveAsDraft));
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : await SaveStepAsync(request, DetailsStep, "request details", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>Step 2 Next (and Est. Value edits on step 3): the selected assets and their estimated values. The list replaces the saved one.</summary>
    private static async Task<IResult> UpdateAssetsAsync(
        string id,
        AssetDisposalAssetsRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var selections = (body.Assets ?? [])
            .Where(selection => !string.IsNullOrWhiteSpace(selection.AssetRegistrationId))
            .GroupBy(selection => selection.AssetRegistrationId!.Trim(), StringComparer.Ordinal)
            .Select(group => (Id: group.Key, EstimatedValue: group.Last().EstimatedValue ?? 0m))
            .ToList();

        if (selections.Count > AssetDisposalValidationExtensions.MaxAssets)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Assets"] = [$"At most {AssetDisposalValidationExtensions.MaxAssets} assets can be in one disposal request"]
            });
        }

        var (assets, errors) = await LoadAssetsAsync(request, selections.Select(selection => selection.Id).ToList(), repository, assetRepository, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        request.Assets = selections
            .Select(selection => ToDisposalAsset(assets[selection.Id], selection.EstimatedValue))
            .ToList();

        request.ValidateAssets(errors, IsDraftSave(request, saveAsDraft));
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : await SaveStepAsync(request, AssetsStep, $"{request.Assets.Count} assets selected", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>Add by Tag: adds the assets with these tags or serial numbers (estimated value 0) to the selection.</summary>
    private static async Task<IResult> AddAssetsByTagAsync(
        string id,
        AddAssetDisposalAssetsByTagRequest body,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tags = ContractText.CleanList(body.Tags, StringComparer.Ordinal);
        if (tags.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Tags"] = ["Enter at least one asset tag"]
            });
        }

        return await AddAssetsAsync(id, tags, "added by tag", repository, assetRepository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>Import from Excel: an .xlsx or .csv (form field "file") with an "Asset Tag" column, or tags in the first column.</summary>
    private static async Task<IResult> ImportAssetsAsync(
        string id,
        IFormFile? file,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (tags, fileError) = await AssetTagFileReader.ReadAsync(file, cancellationToken);
        if (fileError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["File"] = [fileError]
            });
        }

        return await AddAssetsAsync(id, tags, $"imported from {file!.FileName}", repository, assetRepository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Adds the assets matching the tags. Tags that match nothing, and assets that are disposed or in another open
    /// request, are reported back rather than failing the whole call.
    /// </summary>
    private static async Task<IResult> AddAssetsAsync(
        string id,
        IReadOnlyCollection<string> tags,
        string change,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var found = await assetRepository.FindByTagsOrSerialsAsync(tags, cancellationToken);
        var openRequests = await repository.GetOpenRequestNumbersAsync(found.Select(asset => asset.AssetRegistrationId).ToList(), request.Id, cancellationToken);
        var existing = request.Assets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.Ordinal);

        var matchedTags = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new Dictionary<string, string>(StringComparer.Ordinal);
        var added = 0;

        foreach (var asset in found)
        {
            var tag = tags.FirstOrDefault(value => value == asset.BasicInformation.AssetTag || value == asset.BasicInformation.SerialNumber) ?? asset.AssetRegistrationId;
            matchedTags.Add(tag);

            if (Unavailable(asset, openRequests) is { } reason)
            {
                rejected[tag] = reason;
            }
            else if (existing.Add(asset.AssetRegistrationId))
            {
                request.Assets.Add(ToDisposalAsset(asset, 0m));
                added++;
            }
        }

        var errors = new Dictionary<string, string[]>();
        request.ValidateAssets(errors, saveAsDraft: true);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        request.CurrentStep = Math.Max(request.CurrentStep, AssetsStep);
        if (!await SaveAsync(request, repository, currentUserAccessor.GetCurrentUser()?.UserName, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, $"{Describe(request)} - {added} assets {change}", EventAction.Updated, cancellationToken);
        return Results.Ok(new AssetDisposalAddAssetsResponse(
            AssetDisposalRequestResponse.FromEntity(request),
            added,
            tags.Where(tag => !matchedTags.Contains(tag)).ToList(),
            rejected));
    }

    private static async Task<IResult> UpdateMethodAsync(
        string id,
        AssetDisposalMethodRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        request.Method = body.ToEntity();

        var errors = new Dictionary<string, string[]>();
        request.ValidateMethod(errors, IsDraftSave(request, saveAsDraft));
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : await SaveStepAsync(request, MethodStep, $"method {request.Method.Method}", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>Step 4 Next: approvers in order, the notification switch, attachments and additional remarks.</summary>
    private static async Task<IResult> UpdateApprovalAsync(
        string id,
        AssetDisposalApprovalRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        request.Approvers = (body.Approvers ?? [])
            .Select((approver, index) => new AssetDisposalApprover
            {
                Level = index + 1,
                ApprovalLevel = approver.ApprovalLevel?.Trim() ?? string.Empty,
                Approver = approver.Approver?.ToEntity() ?? new AssetDisposalPerson(),
                ApprovalType = AssetDisposalOptions.ApprovalTypes.NormalizeOrKeep(approver.ApprovalType) ?? AssetDisposalOptions.Mandatory,
                Status = AssetDisposalRequestRepository.PendingApproval
            })
            .ToList();
        request.NotifyApprovers = body.NotifyApprovers;
        request.Attachments = (body.Attachments ?? []).Select(attachment => attachment.ToEntity(userName)).ToList();
        request.Remarks = ContractText.TrimOrNull(body.Remarks);

        var errors = new Dictionary<string, string[]>();
        request.ValidateApproval(errors, IsDraftSave(request, saveAsDraft));
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : await SaveStepAsync(request, ApprovalStep, $"{request.Approvers.Count} approvers, {request.Attachments.Count} attachments", repository, currentUserAccessor, eventLogger, cancellationToken);
    }

    /// <summary>
    /// Submit Request: checks every step and that each asset is still registered, not disposed and not in another
    /// open request, refreshes the asset details, and sends the request to the first approver.
    /// A rejected request starts its approvals again.
    /// </summary>
    private static async Task<IResult> SubmitRequestAsync(
        string id,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindEditableAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var (assets, errors) = await LoadAssetsAsync(
            request,
            request.Assets.Select(asset => asset.AssetRegistrationId).ToList(),
            repository,
            assetRepository,
            cancellationToken,
            keyPrefix: "Assets.");
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        request.Assets = request.Assets
            .Select(asset => ToDisposalAsset(assets[asset.AssetRegistrationId], asset.EstimatedValue))
            .ToList();

        errors = request.ValidateForSubmit();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        foreach (var approver in request.Approvers)
        {
            approver.Status = AssetDisposalRequestRepository.PendingApproval;
            approver.Comments = null;
            approver.ActedBy = null;
            approver.ActedAt = null;
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        var wasRejected = request.Status == AssetDisposalRequestRepository.RejectedStatus;
        request.Status = AssetDisposalRequestRepository.PendingApprovalStatus;
        request.CurrentStep = ReviewStep;
        request.SubmittedBy = userName;
        request.SubmittedAt = DateTime.UtcNow;

        if (!await SaveAsync(request, repository, userName, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(
            EntityType,
            request.Id,
            $"{Describe(request)} {(wasRejected ? "resubmitted" : "submitted")} for approval, {request.Assets.Count} assets",
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    /// <summary>Approves the current level. When no Mandatory level is left pending, the request is Approved and remaining Optional levels are skipped.</summary>
    private static Task<IResult> ApproveAsync(
        string id,
        AssetDisposalDecisionRequest? body,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken) =>
        DecideAsync(id, AssetDisposalRequestRepository.Approved, body?.Comments, repository, currentUserAccessor, eventLogger, cancellationToken);

    /// <summary>Rejects the request at the current level; Comments are required. The requester can edit and resubmit it.</summary>
    private static Task<IResult> RejectAsync(
        string id,
        AssetDisposalDecisionRequest? body,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken) =>
        DecideAsync(id, AssetDisposalRequestRepository.Rejected, body?.Comments, repository, currentUserAccessor, eventLogger, cancellationToken);

    /// <summary>Skips the current level when it is Optional, so the request moves on to the next approver.</summary>
    private static Task<IResult> SkipAsync(
        string id,
        AssetDisposalDecisionRequest? body,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken) =>
        DecideAsync(id, AssetDisposalRequestRepository.Skipped, body?.Comments, repository, currentUserAccessor, eventLogger, cancellationToken);

    private static async Task<IResult> DecideAsync(
        string id,
        string decision,
        string? comments,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        var current = AssetDisposalRequestResponse.CurrentApproverOf(request);
        if (current is null)
        {
            return Results.Conflict(new { message = $"Request {request.RequestNumber} is {request.Status}, not waiting for approval." });
        }

        var errors = new Dictionary<string, string[]>();
        comments = ContractText.TrimOrNull(comments);
        if (decision == AssetDisposalRequestRepository.Rejected && comments is null)
        {
            errors["Comments"] = ["Give a reason for rejecting"];
        }
        else if (comments?.Length > CommentsMaxLength)
        {
            errors["Comments"] = [$"Comments cannot be longer than {CommentsMaxLength} characters"];
        }

        if (decision == AssetDisposalRequestRepository.Skipped && current.ApprovalType == AssetDisposalOptions.Mandatory)
        {
            errors["Level"] = [$"Level {current.Level} ({current.ApprovalLevel}) is Mandatory and cannot be skipped"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Only the level's own approver may approve or reject it, when the caller is signed in and the approver is a user.
        var currentUser = currentUserAccessor.GetCurrentUser();
        if (decision != AssetDisposalRequestRepository.Skipped
            && currentUser is not null
            && current.Approver.UserId is { } approverId
            && approverId != currentUser.UserId
            && approverId != currentUser.Id)
        {
            return Results.Problem(
                $"Level {current.Level} ({current.ApprovalLevel}) is waiting for {current.Approver.Name}.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        current.Status = decision;
        current.Comments = comments;
        current.ActedBy = currentUser?.UserName;
        current.ActedAt = DateTime.UtcNow;

        if (decision == AssetDisposalRequestRepository.Rejected)
        {
            request.Status = AssetDisposalRequestRepository.RejectedStatus;
        }
        else if (request.Approvers.All(approver => approver.Status != AssetDisposalRequestRepository.PendingApproval
            || approver.ApprovalType != AssetDisposalOptions.Mandatory))
        {
            foreach (var optional in request.Approvers.Where(approver => approver.Status == AssetDisposalRequestRepository.PendingApproval))
            {
                optional.Status = AssetDisposalRequestRepository.Skipped;
            }

            request.Status = AssetDisposalRequestRepository.ApprovedStatus;
        }

        if (!await SaveAsync(request, repository, currentUser?.UserName, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(
            EntityType,
            request.Id,
            $"{Describe(request)} - level {current.Level} ({current.ApprovalLevel}) {decision.ToLowerInvariant()} by {currentUser?.UserName ?? current.Approver.Name}; now {request.Status}",
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    /// <summary>Records the actual disposal of an approved request and marks each asset Disposed.</summary>
    private static async Task<IResult> CompleteAsync(
        string id,
        CompleteAssetDisposalRequest body,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status != AssetDisposalRequestRepository.ApprovedStatus)
        {
            return Results.Conflict(new { message = $"Only an approved request can be completed; {request.RequestNumber} is {request.Status}." });
        }

        var errors = body.Validate(request.RequestDate);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        foreach (var disposed in request.Assets)
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(disposed.AssetRegistrationId, cancellationToken);
            if (asset is null)
            {
                continue;
            }

            asset.BasicInformation.Status = AssetDisposalOptions.DisposedAssetStatus;
            asset.UpdatedBy = userName;
            asset.UpdatedAt = DateTime.UtcNow;

            await assetRepository.UpdateAsync(asset.Id, asset, cancellationToken);
            await eventLogger.LogAsync(
                AssetEntityType,
                asset.AssetRegistrationId,
                $"{asset.BasicInformation.AssetName ?? asset.AssetRegistrationId} disposed ({request.Method.Method}) in {request.RequestNumber}",
                EventAction.Updated,
                cancellationToken);
        }

        request.Completion = new AssetDisposalCompletion
        {
            ActualDisposalDate = ContractText.DateOnly(body.ActualDisposalDate),
            RecoveredValue = body.RecoveredValue,
            CertificateNumber = ContractText.TrimOrNull(body.CertificateNumber),
            Certificate = body.Certificate?.ToEntity(userName),
            Remarks = ContractText.TrimOrNull(body.Remarks),
            CompletedBy = userName,
            CompletedAt = DateTime.UtcNow
        };
        request.Status = AssetDisposalRequestRepository.CompletedStatus;

        if (!await SaveAsync(request, repository, userName, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, $"{Describe(request)} completed, {request.Assets.Count} assets disposed", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    /// <summary>Cancels a request that has not been completed. Its assets are untouched.</summary>
    private static async Task<IResult> CancelAsync(
        string id,
        CancelAssetDisposalRequest? body,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status is AssetDisposalRequestRepository.CompletedStatus or AssetDisposalRequestRepository.CancelledStatus)
        {
            return Results.Conflict(new { message = $"Request {request.RequestNumber} is already {request.Status}." });
        }

        var reason = ContractText.TrimOrNull(body?.Reason);
        if (reason?.Length > CommentsMaxLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Reason"] = [$"Reason cannot be longer than {CommentsMaxLength} characters"]
            });
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        request.Status = AssetDisposalRequestRepository.CancelledStatus;
        request.CancelledReason = reason;

        if (!await SaveAsync(request, repository, userName, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, $"{Describe(request)} cancelled", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    /// <summary>Deletes a draft. A submitted request is kept as a record; cancel it instead.</summary>
    private static async Task<IResult> DeleteRequestAsync(
        string id,
        IAssetDisposalRequestRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status != AssetDisposalRequestRepository.DraftStatus)
        {
            return Results.Conflict(new { message = $"Only a draft can be deleted; cancel {request.RequestNumber} instead." });
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return RequestNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, Describe(request), EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static void ApplyDetails(AssetDisposalRequest request, AssetDisposalDetailsRequest body, CurrentUser? currentUser)
    {
        request.RequestTitle = body.RequestTitle?.Trim() ?? string.Empty;
        request.RequestDate = ContractText.DateOnly(body.RequestDate);
        request.RequestedBy = body.RequestedBy?.ToEntity()
            ?? (currentUser is null ? null : new AssetDisposalPerson { UserId = currentUser.UserId ?? currentUser.Id, Name = currentUser.UserName });
        request.Reason = AssetDisposalOptions.Reasons.NormalizeOrKeep(body.Reason);
        request.DisposalCategory = ContractText.TrimOrNull(body.DisposalCategory);
        request.Priority = AssetDisposalOptions.Priorities.NormalizeOrKeep(body.Priority) ?? AssetDisposalOptions.NormalPriority;
        request.Justification = ContractText.TrimOrNull(body.Justification);
        request.Currency = ContractText.TrimOrNull(body.Currency)?.ToUpperInvariant() ?? AssetDisposalOptions.DefaultCurrency;
    }

    /// <summary>Loads the assets by id; each must exist, not be disposed and not be in another open request.</summary>
    private static async Task<(Dictionary<string, AssetRegistration> Assets, Dictionary<string, string[]> Errors)> LoadAssetsAsync(
        AssetDisposalRequest request,
        IReadOnlyList<string> assetIds,
        IAssetDisposalRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken,
        string keyPrefix = "")
    {
        var errors = new Dictionary<string, string[]>();
        var found = await assetRepository.FindInScopeAsync(
            new AssetScopeQuery(null, null, null, null, null, null, null, null, assetIds),
            assetIds.Count + 1,
            cancellationToken);
        var assets = found.ToDictionary(asset => asset.AssetRegistrationId, StringComparer.Ordinal);
        var openRequests = await repository.GetOpenRequestNumbersAsync(assetIds, request.Id, cancellationToken);

        for (var index = 0; index < assetIds.Count; index++)
        {
            var key = $"{keyPrefix}Assets[{index}].AssetRegistrationId";
            if (!assets.TryGetValue(assetIds[index], out var asset))
            {
                errors[key] = [$"Asset '{assetIds[index]}' was not found"];
            }
            else if (Unavailable(asset, openRequests) is { } reason)
            {
                errors[key] = [$"{asset.BasicInformation.AssetTag ?? asset.AssetRegistrationId}: {reason}"];
            }
        }

        return (assets, errors);
    }

    /// <summary>Why the asset cannot be put in this request, or null when it can.</summary>
    private static string? Unavailable(AssetRegistration asset, IReadOnlyDictionary<string, string> openRequests) =>
        string.Equals(asset.BasicInformation.Status, AssetDisposalOptions.DisposedAssetStatus, StringComparison.OrdinalIgnoreCase)
            ? "Asset is already disposed"
            : openRequests.TryGetValue(asset.AssetRegistrationId, out var requestNumber)
                ? $"Asset is already in disposal request {requestNumber}"
                : null;

    private static AssetDisposalAsset ToDisposalAsset(AssetRegistration asset, decimal estimatedValue) => new()
    {
        AssetRegistrationId = asset.AssetRegistrationId,
        AssetName = asset.BasicInformation.AssetName,
        AssetTag = asset.BasicInformation.AssetTag,
        Category = asset.Classification.Category,
        Model = asset.BasicInformation.Model,
        SerialNumber = asset.BasicInformation.SerialNumber,
        Manufacturer = asset.BasicInformation.Manufacturer,
        Location = asset.LocationAssignment.Location.LocationPath,
        Department = asset.LocationAssignment.Assignment.CustodianDepartment ?? asset.Classification.Department,
        Condition = asset.BasicInformation.Condition,
        Status = asset.BasicInformation.Status,
        ImageUrl = asset.BasicInformation.Images.FirstOrDefault(),
        EstimatedValue = estimatedValue
    };

    private static async Task<IResult> SaveStepAsync(
        AssetDisposalRequest request,
        int step,
        string change,
        IAssetDisposalRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        request.CurrentStep = Math.Max(request.CurrentStep, step);
        if (!await SaveAsync(request, repository, currentUserAccessor.GetCurrentUser()?.UserName, cancellationToken))
        {
            return RequestNotFound(request.Id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, $"{Describe(request)} - {change}", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetDisposalRequestResponse.FromEntity(request));
    }

    private static async Task<bool> SaveAsync(AssetDisposalRequest request, IAssetDisposalRequestRepository repository, string? userName, CancellationToken cancellationToken)
    {
        request.UpdatedBy = userName;
        request.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(request.Id, request, cancellationToken);
    }

    private static async Task<AssetDisposalRequest?> FindAsync(string id, IAssetDisposalRequestRepository repository, CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(id, cancellationToken);
        return request is null || request.IsDeleted ? null : request;
    }

    /// <summary>Drafts and rejected requests can be changed; anything else is with the approvers or done.</summary>
    private static async Task<(AssetDisposalRequest? Request, IResult? Problem)> FindEditableAsync(
        string id,
        IAssetDisposalRequestRepository repository,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return (null, RequestNotFound(id));
        }

        return request.Status is AssetDisposalRequestRepository.DraftStatus or AssetDisposalRequestRepository.RejectedStatus
            ? (request, null)
            : (null, Results.Conflict(new { message = $"Request {request.RequestNumber} is {request.Status} and can no longer be changed." }));
    }

    /// <summary>Save as Draft only relaxes the rules while the request is a draft; a rejected one being fixed must stay complete.</summary>
    private static bool IsDraftSave(AssetDisposalRequest request, bool? saveAsDraft) =>
        saveAsDraft == true && request.Status == AssetDisposalRequestRepository.DraftStatus;

    private static string Describe(AssetDisposalRequest request) => $"{request.RequestNumber} - {request.RequestTitle}";

    private static IResult RequestNotFound(string id) =>
        Results.NotFound(new { message = $"Disposal request '{id}' was not found." });
}
