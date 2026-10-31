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

public static class AssetRegistrationEndpoints
{
    private const string SequenceName = "asset-registration";
    private const string AssetRegistrationIdPrefix = "AR";
    private const string DraftEntityType = "AssetRegistrationDraft";
    private const string AssetRegistrationEntityType = "AssetRegistration";

    private const int BasicInformationStep = 1;
    private const int ClassificationStep = 2;
    private const int LocationAssignmentStep = 3;
    private const int PurchaseWarrantyStep = 4;
    private const int AdditionalInformationStep = 5;

    public static RouteGroupBuilder MapAssetRegistrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations").WithTags("Asset Registration");

        // Fixed dropdown choices for all five steps; master data dropdowns use their own Master Management lists.
        group.MapGet("/options", () => Results.Ok(AssetRegistrationOptionsResponse.Instance)).WithName("GetAssetRegistrationOptions");

        // Drafts: every step is saved here; submitting after step 5 moves the whole draft into AssetRegistration.
        group.MapPost("/drafts", CreateDraftAsync).WithName("CreateAssetRegistrationDraft");
        group.MapGet("/drafts", GetDraftsAsync).WithName("GetAssetRegistrationDrafts");
        group.MapGet("/drafts/{assetRegistrationId}", GetDraftAsync).WithName("GetAssetRegistrationDraft");
        group.MapDelete("/drafts/{assetRegistrationId}", DeleteDraftAsync).WithName("DeleteAssetRegistrationDraft");

        // Step 1 of 5: Basic Information.
        group.MapPut("/drafts/{assetRegistrationId}/basic-information", UpdateBasicInformationAsync).WithName("UpdateAssetRegistrationDraftBasicInformation");

        // Step 2 of 5: Classification.
        group.MapPut("/drafts/{assetRegistrationId}/classification", UpdateClassificationAsync).WithName("UpdateAssetRegistrationDraftClassification");

        // Step 3 of 5: Location & Assignment.
        group.MapPut("/drafts/{assetRegistrationId}/location-assignment", UpdateLocationAssignmentAsync).WithName("UpdateAssetRegistrationDraftLocationAssignment");

        // Step 4 of 5: Purchase & Warranty.
        group.MapPut("/drafts/{assetRegistrationId}/purchase-warranty", UpdatePurchaseWarrantyAsync).WithName("UpdateAssetRegistrationDraftPurchaseWarranty");

        // Step 5 of 5: Additional Information.
        group.MapPut("/drafts/{assetRegistrationId}/additional-information", UpdateAdditionalInformationAsync).WithName("UpdateAssetRegistrationDraftAdditionalInformation");

        // Submit: checks every step, then moves the draft into AssetRegistration under the same id.
        group.MapPost("/drafts/{assetRegistrationId}/submit", SubmitDraftAsync).WithName("SubmitAssetRegistrationDraft");

        // Registered assets.
        group.MapGet("/", GetAssetRegistrationsAsync).WithName("GetAssetRegistrations");
        group.MapGet("/filters", GetAssetListFiltersAsync).WithName("GetAssetRegistrationListFilters");
        group.MapGet("/{assetRegistrationId}", GetAssetRegistrationAsync).WithName("GetAssetRegistration");

        return group;
    }

    private static async Task<IResult> CreateDraftAsync(
        CreateAssetRegistrationDraftRequest request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        IAssetRegistrationRepository assetRegistrationRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.BasicInformation.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var basicInformation = (request.BasicInformation ?? EmptyBasicInformation).ToSection();

        var assetTagConflict = await CheckAssetTagAsync(basicInformation.AssetTag, null, draftRepository, assetRegistrationRepository, cancellationToken);
        if (assetTagConflict is not null)
        {
            return assetTagConflict;
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var currentUser = currentUserAccessor.GetCurrentUser();

        var draft = await draftRepository.CreateAsync(
            new AssetRegistrationDraft
            {
                AssetRegistrationId = $"{AssetRegistrationIdPrefix}{nextSequence:D6}",
                CurrentStep = BasicInformationStep,
                BasicInformation = basicInformation,
                CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
                TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
                IsDeleted = false
            },
            cancellationToken);

        await eventLogger.LogAsync(DraftEntityType, draft.AssetRegistrationId, basicInformation.AssetName ?? draft.AssetRegistrationId, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-registrations/drafts/{draft.AssetRegistrationId}",
            AssetRegistrationDraftResponse.FromEntity(draft));
    }

    private static async Task<IResult> GetDraftsAsync(
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        // Signed-in users see their own drafts, newest first.
        var drafts = await draftRepository.GetRecentAsync(currentUserAccessor.GetCurrentUser()?.UserName, cancellationToken);
        return Results.Ok(drafts.Select(AssetRegistrationDraftResponse.FromEntity));
    }

    private static async Task<IResult> GetDraftAsync(
        string assetRegistrationId,
        IAssetRegistrationDraftRepository draftRepository,
        CancellationToken cancellationToken)
    {
        var draft = await draftRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        return draft is null
            ? DraftNotFound(assetRegistrationId)
            : Results.Ok(AssetRegistrationDraftResponse.FromEntity(draft));
    }

    private static async Task<IResult> DeleteDraftAsync(
        string assetRegistrationId,
        IAssetRegistrationDraftRepository draftRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!await draftRepository.DeleteByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken))
        {
            return DraftNotFound(assetRegistrationId);
        }

        await eventLogger.LogAsync(DraftEntityType, assetRegistrationId, assetRegistrationId, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateBasicInformationAsync(
        string assetRegistrationId,
        AssetBasicInformationDto request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        IAssetRegistrationRepository assetRegistrationRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var basicInformation = request.ToSection();

        var assetTagConflict = await CheckAssetTagAsync(basicInformation.AssetTag, assetRegistrationId, draftRepository, assetRegistrationRepository, cancellationToken);
        if (assetTagConflict is not null)
        {
            return assetTagConflict;
        }

        return await SaveSectionAsync(
            assetRegistrationId,
            draft => draft.BasicInformation,
            basicInformation,
            BasicInformationStep,
            draftRepository,
            currentUserAccessor,
            eventLogger,
            cancellationToken);
    }

    private static async Task<IResult> UpdateClassificationAsync(
        string assetRegistrationId,
        AssetClassificationDto request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return await SaveSectionAsync(
            assetRegistrationId,
            draft => draft.Classification,
            request.ToSection(),
            ClassificationStep,
            draftRepository,
            currentUserAccessor,
            eventLogger,
            cancellationToken);
    }

    private static async Task<IResult> UpdateLocationAssignmentAsync(
        string assetRegistrationId,
        AssetLocationAssignmentDto request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return await SaveSectionAsync(
            assetRegistrationId,
            draft => draft.LocationAssignment,
            request.ToSection(),
            LocationAssignmentStep,
            draftRepository,
            currentUserAccessor,
            eventLogger,
            cancellationToken);
    }

    private static async Task<IResult> UpdatePurchaseWarrantyAsync(
        string assetRegistrationId,
        AssetPurchaseWarrantyDto request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return await SaveSectionAsync(
            assetRegistrationId,
            draft => draft.PurchaseWarranty,
            request.ToSection(),
            PurchaseWarrantyStep,
            draftRepository,
            currentUserAccessor,
            eventLogger,
            cancellationToken);
    }

    private static async Task<IResult> UpdateAdditionalInformationAsync(
        string assetRegistrationId,
        AssetAdditionalInformationDto request,
        [FromQuery] bool? saveAsDraft,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(saveAsDraft ?? false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return await SaveSectionAsync(
            assetRegistrationId,
            draft => draft.AdditionalInformation,
            request.ToSection(),
            AdditionalInformationStep,
            draftRepository,
            currentUserAccessor,
            eventLogger,
            cancellationToken);
    }

    private static async Task<IResult> SubmitDraftAsync(
        string assetRegistrationId,
        IAssetRegistrationDraftRepository draftRepository,
        IAssetRegistrationRepository assetRegistrationRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var draft = await draftRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (draft is null)
        {
            // A retried submit whose draft was already moved returns the registered asset.
            var registered = await assetRegistrationRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
            return registered is null
                ? DraftNotFound(assetRegistrationId)
                : Results.Ok(AssetRegistrationResponse.FromEntity(registered));
        }

        var validationErrors = draft.ValidateForSubmit();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        // Insert before deleting the draft: if the delete fails, a retry finds the asset already registered and finishes the move.
        var asset = await assetRegistrationRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (asset is null)
        {
            var assetTagConflict = await CheckAssetTagAsync(draft.BasicInformation!.AssetTag, assetRegistrationId, draftRepository, assetRegistrationRepository, cancellationToken);
            if (assetTagConflict is not null)
            {
                return assetTagConflict;
            }

            var currentUser = currentUserAccessor.GetCurrentUser();

            asset = await assetRegistrationRepository.CreateAsync(
                new AssetRegistration
                {
                    AssetRegistrationId = draft.AssetRegistrationId,
                    BasicInformation = draft.BasicInformation,
                    Classification = draft.Classification!,
                    LocationAssignment = draft.LocationAssignment!,
                    PurchaseWarranty = draft.PurchaseWarranty!,
                    AdditionalInformation = draft.AdditionalInformation ?? new AssetAdditionalInformation(),
                    CreatedBy = currentUser?.UserName ?? draft.CreatedBy,
                    CreatedAt = DateTime.UtcNow,
                    ClientId = draft.ClientId,
                    TenantId = draft.TenantId,
                    IsDeleted = false
                },
                cancellationToken);

            await eventLogger.LogAsync(AssetRegistrationEntityType, asset.AssetRegistrationId, asset.BasicInformation.AssetName ?? asset.AssetRegistrationId, EventAction.Created, cancellationToken);
        }

        await draftRepository.DeleteByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);

        return Results.Created(
            $"/api/asset-registrations/{asset.AssetRegistrationId}",
            AssetRegistrationResponse.FromEntity(asset));
    }

    private static async Task<IResult> GetAssetListFiltersAsync(
        IAssetRegistrationRepository assetRegistrationRepository,
        CancellationToken cancellationToken)
    {
        var assets = await assetRegistrationRepository.GetFilterFieldsAsync(cancellationToken);
        return Results.Ok(AssetListFilterOptionsResponse.FromAssets(assets));
    }

    private static async Task<IResult> GetAssetRegistrationsAsync(
        IAssetRegistrationRepository assetRegistrationRepository,
        CancellationToken cancellationToken,
        string? search = null,
        string? category = null,
        string? assetType = null,
        string? status = null,
        string? locationId = null,
        string? department = null,
        string? custodianUserId = null,
        string? criticality = null,
        string? manufacturer = null,
        string? condition = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = AssetRegistrationRepository.DefaultPageSize)
    {
        var sortDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var result = await assetRegistrationRepository.QueryAsync(
            new AssetRegistrationQuery(search, category, assetType, status, locationId, department, custodianUserId, criticality, manufacturer, sortBy, sortDescending, page, pageSize, condition),
            cancellationToken);

        return Results.Ok(AssetRegistrationListResponse.FromResult(
            result,
            page < 1 ? 1 : page,
            pageSize is < 1 or > AssetRegistrationRepository.MaxPageSize ? AssetRegistrationRepository.DefaultPageSize : pageSize));
    }

    private static async Task<IResult> GetAssetRegistrationAsync(
        string assetRegistrationId,
        IAssetRegistrationRepository assetRegistrationRepository,
        CancellationToken cancellationToken)
    {
        var asset = await assetRegistrationRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        return asset is null
            ? Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." })
            : Results.Ok(AssetRegistrationResponse.FromEntity(asset));
    }

    private static async Task<IResult> SaveSectionAsync<TSection>(
        string assetRegistrationId,
        System.Linq.Expressions.Expression<Func<AssetRegistrationDraft, TSection?>> section,
        TSection value,
        int step,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var draft = await draftRepository.UpdateSectionAsync(
            assetRegistrationId,
            section,
            value,
            step,
            currentUserAccessor.GetCurrentUser()?.UserName,
            cancellationToken);

        if (draft is null)
        {
            return DraftNotFound(assetRegistrationId);
        }

        await eventLogger.LogAsync(DraftEntityType, draft.AssetRegistrationId, draft.BasicInformation?.AssetName ?? draft.AssetRegistrationId, EventAction.Updated, cancellationToken);
        return Results.Ok(AssetRegistrationDraftResponse.FromEntity(draft));
    }

    // An asset tag must be unique across registered assets and every other draft.
    private static async Task<IResult?> CheckAssetTagAsync(
        string? assetTag,
        string? assetRegistrationId,
        IAssetRegistrationDraftRepository draftRepository,
        IAssetRegistrationRepository assetRegistrationRepository,
        CancellationToken cancellationToken)
    {
        if (assetTag is null)
        {
            return null;
        }

        if (await assetRegistrationRepository.AssetTagExistsAsync(assetTag, cancellationToken))
        {
            return Results.Conflict(new { message = $"An asset with tag '{assetTag}' is already registered." });
        }

        if (await draftRepository.AssetTagExistsAsync(assetTag, assetRegistrationId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Asset tag '{assetTag}' is already used by another draft registration." });
        }

        return null;
    }

    private static IResult DraftNotFound(string assetRegistrationId)
    {
        return Results.NotFound(new { message = $"Asset registration draft '{assetRegistrationId}' was not found." });
    }

    private static readonly AssetBasicInformationDto EmptyBasicInformation =
        new(null, null, null, null, null, null, null, null, null, null, null, null);
}
