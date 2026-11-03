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

/// <summary>The Assign Asset wizard: assigns one or more assets to an employee, department, location/site or other entity.</summary>
/// <remarks>
/// Step 1 picks assets from GET /api/asset-registrations (Single, Multiple and Scan all send the chosen ids).
/// Attachments are uploaded through the Media service first; only their links are sent here.
/// View History reads GET /api/eventlogs?entityType=AssetAssignment&amp;entityId={id}.
/// </remarks>
public static class AssetAssignmentEndpoints
{
    private const string EntityType = "AssetAssignment";
    private const string AssetEntityType = "AssetRegistration";
    private const string SequenceName = "asset-assignment";
    private const string AssignmentNumberPrefix = "ASG-";

    // Set on each asset while it is in an active assignment.
    private const string AssignedAssetStatus = "Assigned";

    // Given back to a released asset whose status before assignment is unknown.
    private const string AvailableAssetStatus = "Available";

    public static RouteGroupBuilder MapAssetAssignmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-assignments").WithTags("Asset Assignments");

        group.MapGet("", GetAssignmentsAsync).WithName("GetAssetAssignments");
        group.MapGet("/options", () => Results.Ok(new AssetAssignmentOptionsResponse(
            AssetAssignmentTargets.All,
            AssetAssignmentOtherEntityTypes.All,
            AssetAssignmentSelectionModes.All))).WithName("GetAssetAssignmentOptions");
        group.MapGet("/{id}", GetAssignmentByIdAsync).WithName("GetAssetAssignmentById");
        group.MapPost("", CreateAssignmentAsync).WithName("CreateAssetAssignment");
        group.MapPut("/{id}", UpdateAssignmentAsync).WithName("UpdateAssetAssignment");
        group.MapDelete("/{id}", DeleteAssignmentAsync).WithName("DeleteAssetAssignment");
        group.MapPost("/bulk-delete", DeleteAssignmentsAsync).WithName("DeleteAssetAssignments");

        return group;
    }

    private static async Task<IResult> GetAssignmentsAsync(
        IAssetAssignmentRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assignTo = null,
        string? status = null,
        string? assetRegistrationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetAssignmentRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetAssignmentRepository.MaxPageSize ? AssetAssignmentRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetAssignmentQuery(
                search,
                assignTo,
                status,
                assetRegistrationId,
                ContractText.DateOnly(fromDate),
                ContractText.DateOnly(toDate),
                page,
                pageSize),
            cancellationToken);

        return Results.Ok(new AssetAssignmentListResponse(
            result.Items.Select(AssetAssignmentResponse.FromEntity).ToList(),
            result.TotalCount,
            result.StatusCounts,
            page,
            pageSize));
    }

    private static async Task<IResult> GetAssignmentByIdAsync(
        string id,
        IAssetAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(id, cancellationToken);

        return assignment is null || assignment.IsDeleted
            ? AssignmentNotFound(id)
            : Results.Ok(AssetAssignmentResponse.FromEntity(assignment));
    }

    /// <summary>
    /// Confirm Assignment, or Save as Draft with ?saveAsDraft=true.
    /// Confirming checks that no selected asset is already assigned, then records the assignment on each asset.
    /// </summary>
    private static async Task<IResult> CreateAssignmentAsync(
        CreateAssetAssignmentRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var isDraft = saveAsDraft ?? false;

        var validationErrors = request.Validate(isDraft);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var (assets, error) = await LoadAssetsAsync(request, isDraft, null, repository, assetRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var currentUser = currentUserAccessor.GetCurrentUser();

        var assignment = new AssetAssignmentRecord
        {
            AssignmentNumber = $"{AssignmentNumberPrefix}{nextSequence:D6}",
            CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
        CopyRequest(assignment, request, assets, isDraft, previousAssets: []);

        await repository.CreateAsync(assignment, cancellationToken);
        await eventLogger.LogAsync(EntityType, assignment.Id, Describe(assignment), EventAction.Created, cancellationToken);

        if (!isDraft)
        {
            await AssignAssetsAsync(assets, assignment, currentUser?.UserName, assetRepository, eventLogger, cancellationToken);
        }

        return Results.Created(
            $"/api/asset-assignments/{assignment.Id}",
            AssetAssignmentResponse.FromEntity(assignment));
    }

    /// <summary>
    /// Replaces the whole assignment with the wizard's values. A draft can be saved again or confirmed;
    /// a confirmed assignment stays confirmed, and assets taken out of it are released.
    /// </summary>
    private static async Task<IResult> UpdateAssignmentAsync(
        string id,
        CreateAssetAssignmentRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(id, cancellationToken);
        if (assignment is null || assignment.IsDeleted)
        {
            return AssignmentNotFound(id);
        }

        var wasActive = IsActive(assignment);
        var isDraft = saveAsDraft ?? false;
        if (wasActive && isDraft)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["SaveAsDraft"] = ["A confirmed assignment cannot be saved as a draft"]
            });
        }

        var validationErrors = request.Validate(isDraft);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var (assets, error) = await LoadAssetsAsync(request, isDraft, id, repository, assetRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var previousAssets = assignment.Assets;
        var currentUser = currentUserAccessor.GetCurrentUser();

        CopyRequest(assignment, request, assets, isDraft, previousAssets);
        assignment.UpdatedBy = currentUser?.UserName;
        assignment.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(id, assignment, cancellationToken))
        {
            return AssignmentNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, assignment.Id, Describe(assignment), EventAction.Updated, cancellationToken);

        if (!isDraft)
        {
            if (wasActive)
            {
                var kept = assets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.Ordinal);
                await ReleaseAssetsAsync(
                    previousAssets.Where(asset => !kept.Contains(asset.AssetRegistrationId)),
                    assignment,
                    currentUser?.UserName,
                    assetRepository,
                    eventLogger,
                    cancellationToken);
            }

            await AssignAssetsAsync(assets, assignment, currentUser?.UserName, assetRepository, eventLogger, cancellationToken);
        }

        return Results.Ok(AssetAssignmentResponse.FromEntity(assignment));
    }

    /// <summary>Deletes the assignment; the assets of a confirmed one are released.</summary>
    private static async Task<IResult> DeleteAssignmentAsync(
        string id,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        return await DeleteOneAsync(id, repository, assetRepository, currentUserAccessor, eventLogger, cancellationToken)
            ? Results.NoContent()
            : AssignmentNotFound(id);
    }

    /// <summary>Deletes the assignments ticked in the list. Ids that are not found are skipped.</summary>
    private static async Task<IResult> DeleteAssignmentsAsync(
        DeleteAssetAssignmentsRequest request,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(request.Ids, StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Ids"] = ["Select at least one assignment"]
            });
        }

        var deleted = 0;
        foreach (var id in ids)
        {
            if (await DeleteOneAsync(id, repository, assetRepository, currentUserAccessor, eventLogger, cancellationToken))
            {
                deleted++;
            }
        }

        return Results.Ok(new { deletedCount = deleted });
    }

    private static async Task<bool> DeleteOneAsync(
        string id,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(id, cancellationToken);
        if (assignment is null || assignment.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
        {
            return false;
        }

        await eventLogger.LogAsync(EntityType, assignment.Id, Describe(assignment), EventAction.Deleted, cancellationToken);

        if (IsActive(assignment))
        {
            await ReleaseAssetsAsync(
                assignment.Assets,
                assignment,
                currentUserAccessor.GetCurrentUser()?.UserName,
                assetRepository,
                eventLogger,
                cancellationToken);
        }

        return true;
    }

    /// <summary>Loads every selected asset so nothing is saved when one is missing; when confirming, none may be assigned elsewhere.</summary>
    private static async Task<(List<AssetRegistration> Assets, IResult? Error)> LoadAssetsAsync(
        CreateAssetAssignmentRequest request,
        bool isDraft,
        string? assignmentId,
        IAssetAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var assetIds = ContractText.CleanList(request.AssetRegistrationIds, StringComparer.Ordinal);
        var assets = new List<AssetRegistration>();
        foreach (var assetRegistrationId in assetIds)
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
            if (asset is null)
            {
                return ([], Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AssetRegistrationIds"] = [$"Asset '{assetRegistrationId}' was not found"]
                }));
            }

            assets.Add(asset);
        }

        if (!isDraft)
        {
            var alreadyAssigned = await repository.GetAssignedAssetIdsAsync(assetIds, DateTime.UtcNow.Date, assignmentId, cancellationToken);
            if (alreadyAssigned.Count > 0)
            {
                return ([], Results.Conflict(new
                {
                    message = $"These assets are already assigned: {string.Join(", ", alreadyAssigned)}.",
                    assetRegistrationIds = alreadyAssigned
                }));
            }
        }

        return (assets, null);
    }

    /// <summary>
    /// Copies the wizard's values onto the record. An asset that was already in it keeps the status it had
    /// before it was first assigned, rather than "Assigned".
    /// </summary>
    private static void CopyRequest(
        AssetAssignmentRecord assignment,
        CreateAssetAssignmentRequest request,
        IReadOnlyCollection<AssetRegistration> assets,
        bool isDraft,
        IReadOnlyCollection<AssetAssignmentAsset> previousAssets)
    {
        var previousStatuses = previousAssets
            .GroupBy(asset => asset.AssetRegistrationId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().PreviousStatus, StringComparer.Ordinal);

        var assignTo = AssetAssignmentTargets.Normalize(request.AssignTo) ?? string.Empty;

        assignment.SelectionMode = AssetAssignmentSelectionModes.Normalize(request.SelectionMode);
        assignment.Assets = assets
            .Select(asset =>
            {
                var copy = AssetAssignmentAssets.FromAsset(asset);
                if (previousStatuses.TryGetValue(asset.AssetRegistrationId, out var previousStatus))
                {
                    copy.PreviousStatus = previousStatus;
                }

                return copy;
            })
            .ToList();
        assignment.AssignTo = assignTo;
        assignment.AssignmentType = ContractText.TrimOrNull(request.AssignmentType);
        assignment.FromDate = ContractText.DateOnly(request.FromDate);
        assignment.ToDate = request.IsOpenEnded ? null : ContractText.DateOnly(request.ToDate);
        assignment.IsOpenEnded = request.IsOpenEnded;
        assignment.Employee = assignTo == AssetAssignmentTargets.Employee ? request.Employee?.ToEntity() : null;
        assignment.Department = assignTo == AssetAssignmentTargets.Department ? request.Department?.ToEntity() : null;
        assignment.OtherEntity = assignTo == AssetAssignmentTargets.OtherEntity ? request.OtherEntity?.ToEntity() : null;
        assignment.Location = request.Location?.ToEntity();
        assignment.CostCenter = ContractText.TrimOrNull(request.CostCenter);
        assignment.Purpose = ContractText.TrimOrNull(request.Purpose);
        assignment.ApprovalReference = ContractText.TrimOrNull(request.ApprovalReference);
        assignment.Remarks = ContractText.TrimOrNull(request.Remarks);
        assignment.Notes = ContractText.TrimOrNull(request.Notes);
        assignment.Attachments = (request.Attachments ?? []).Select(attachment => attachment.ToEntity()).ToList();
        assignment.Notification = request.Notification?.ToEntity() ?? new AssetAssignmentNotification();
        assignment.Reminder = request.Reminder?.ToEntity() ?? new AssetAssignmentReminder();
        assignment.CustomFields = request.CustomFields?.ToEntity() ?? new AssetAssignmentCustomFields();
        assignment.Status = isDraft ? AssetAssignmentRepository.DraftStatus : AssetAssignmentRepository.ActiveStatus;
    }

    /// <summary>Copies the confirmed assignment onto each asset's Location &amp; Assignment section and marks it assigned.</summary>
    /// <remarks>Also used by the Bulk Assignment wizard.</remarks>
    internal static async Task AssignAssetsAsync(
        IEnumerable<AssetRegistration> assets,
        AssetAssignmentRecord assignment,
        string? userName,
        IAssetRegistrationRepository assetRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        foreach (var asset in assets)
        {
            var current = asset.LocationAssignment.Assignment;
            current.CustodianDepartment = assignment.Employee?.DepartmentName
                ?? assignment.Department?.DepartmentName
                ?? current.CustodianDepartment;
            current.AssignedToUserId = assignment.AssignTo == AssetAssignmentTargets.Employee ? assignment.Employee?.EmployeeId : null;
            current.AssignedToName = AssetAssignmentAssets.AssignedToName(assignment);
            current.CostCenter = assignment.CostCenter ?? current.CostCenter;
            current.AssignmentStartDate = assignment.FromDate;
            current.AssignmentEndDate = assignment.ToDate;
            current.IsCurrent = true;

            asset.BasicInformation.Status = AssignedAssetStatus;
            asset.UpdatedBy = userName;
            asset.UpdatedAt = DateTime.UtcNow;

            await assetRepository.UpdateAsync(asset.Id, asset, cancellationToken);
            await eventLogger.LogAsync(
                AssetEntityType,
                asset.AssetRegistrationId,
                $"{asset.BasicInformation.AssetName ?? asset.AssetRegistrationId} assigned in {assignment.AssignmentNumber}",
                EventAction.Updated,
                cancellationToken);
        }
    }

    /// <summary>
    /// Ends the asset's current assignment and gives back the status it had before.
    /// An asset whose status was changed since (e.g. to "Maintenance") keeps that status.
    /// </summary>
    private static async Task ReleaseAssetsAsync(
        IEnumerable<AssetAssignmentAsset> assignedAssets,
        AssetAssignmentRecord assignment,
        string? userName,
        IAssetRegistrationRepository assetRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        foreach (var assignedAsset in assignedAssets)
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(assignedAsset.AssetRegistrationId, cancellationToken);
            if (asset is null)
            {
                continue;
            }

            var current = asset.LocationAssignment.Assignment;
            current.IsCurrent = false;
            current.AssignmentEndDate ??= DateTime.UtcNow.Date;

            if (string.Equals(asset.BasicInformation.Status, AssignedAssetStatus, StringComparison.OrdinalIgnoreCase))
            {
                asset.BasicInformation.Status = assignedAsset.PreviousStatus is { Length: > 0 } previousStatus
                    && !string.Equals(previousStatus, AssignedAssetStatus, StringComparison.OrdinalIgnoreCase)
                        ? previousStatus
                        : AvailableAssetStatus;
            }

            asset.UpdatedBy = userName;
            asset.UpdatedAt = DateTime.UtcNow;

            await assetRepository.UpdateAsync(asset.Id, asset, cancellationToken);
            await eventLogger.LogAsync(
                AssetEntityType,
                asset.AssetRegistrationId,
                $"{asset.BasicInformation.AssetName ?? asset.AssetRegistrationId} released from {assignment.AssignmentNumber}",
                EventAction.Updated,
                cancellationToken);
        }
    }

    private static bool IsActive(AssetAssignmentRecord assignment) =>
        string.Equals(assignment.Status, AssetAssignmentRepository.ActiveStatus, StringComparison.Ordinal);

    private static string Describe(AssetAssignmentRecord assignment) =>
        $"{assignment.AssignmentNumber} - {assignment.Assets.Count} assets to {assignment.AssignTo} {AssetAssignmentAssets.AssignedToName(assignment)}".TrimEnd();

    private static IResult AssignmentNotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset assignment '{id}' was not found." });
    }
}
