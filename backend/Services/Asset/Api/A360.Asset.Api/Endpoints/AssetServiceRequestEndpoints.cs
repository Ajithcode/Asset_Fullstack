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

/// <summary>Service requests raised on an asset: the New Service Request form and the Details, Updates, Attachments and Related tabs.</summary>
/// <remarks>
/// Submit Service Request creates the request as Open (Save as Draft keeps it a Draft until submitted).
/// Status changes are made by adding an update with a Status, so they appear on the Updates timeline.
/// Work orders and spare parts belong to the Maintenance service: they are linked by number and read from there.
/// Convert to Work Order creates the work order with POST /api/work-orders (Maintenance), then links it here.
/// Files are uploaded through the Media service first; only their links are sent here.
/// </remarks>
public static class AssetServiceRequestEndpoints
{
    private const string EntityType = "AssetServiceRequest";
    private const string SequencePrefix = "asset-service-request-";
    private const string RequestNumberPrefix = "SR-";

    public static RouteGroupBuilder MapAssetServiceRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/service-requests").WithTags("Asset Service Requests");

        group.MapGet("", GetRequestsAsync).WithName("GetAssetServiceRequests");
        group.MapGet("/options", GetOptions).WithName("GetAssetServiceRequestOptions");

        // New Service Request form, Edit and Submit.
        group.MapPost("", CreateRequestAsync).WithName("CreateAssetServiceRequest");
        group.MapGet("/{id}", GetRequestAsync).WithName("GetAssetServiceRequest");
        group.MapPut("/{id}", UpdateRequestAsync).WithName("UpdateAssetServiceRequest");
        group.MapPost("/{id}/submit", SubmitRequestAsync).WithName("SubmitAssetServiceRequest");
        group.MapDelete("/{id}", DeleteRequestAsync).WithName("DeleteAssetServiceRequest");

        // Updates tab (comments and status changes).
        group.MapGet("/{id}/updates", GetUpdatesAsync).WithName("GetAssetServiceRequestUpdates");
        group.MapPost("/{id}/updates", AddUpdateAsync).WithName("AddAssetServiceRequestUpdate");

        // Attachments tab.
        group.MapGet("/{id}/attachments", GetAttachmentsAsync).WithName("GetAssetServiceRequestAttachments");
        group.MapPost("/{id}/attachments", AddAttachmentsAsync).WithName("AddAssetServiceRequestAttachments");
        group.MapDelete("/{id}/attachments/{attachmentId}", DeleteAttachmentAsync).WithName("DeleteAssetServiceRequestAttachment");
        group.MapPost("/{id}/attachments/bulk-delete", DeleteAttachmentsAsync).WithName("DeleteAssetServiceRequestAttachments");

        // Related tab.
        group.MapGet("/{id}/related", GetRelatedAsync).WithName("GetAssetServiceRequestRelated");
        group.MapPost("/{id}/work-orders", LinkWorkOrderAsync).WithName("LinkAssetServiceRequestWorkOrder");
        group.MapDelete("/{id}/work-orders/{workOrderId}", UnlinkWorkOrderAsync).WithName("UnlinkAssetServiceRequestWorkOrder");
        group.MapPost("/{id}/convert-to-work-order", ConvertToWorkOrderAsync).WithName("ConvertAssetServiceRequestToWorkOrder");
        group.MapPost("/{id}/related-assets", LinkRelatedAssetAsync).WithName("LinkAssetServiceRequestRelatedAsset");
        group.MapDelete("/{id}/related-assets/{referenceId}", UnlinkRelatedAssetAsync).WithName("UnlinkAssetServiceRequestRelatedAsset");
        group.MapPost("/{id}/spare-parts", LinkSparePartAsync).WithName("LinkAssetServiceRequestSparePart");
        group.MapDelete("/{id}/spare-parts/{partId}", UnlinkSparePartAsync).WithName("UnlinkAssetServiceRequestSparePart");
        group.MapPost("/{id}/documents", LinkDocumentAsync).WithName("LinkAssetServiceRequestDocument");
        group.MapDelete("/{id}/documents/{documentId}", UnlinkDocumentAsync).WithName("UnlinkAssetServiceRequestDocument");

        return group;
    }

    private static IResult GetOptions() => Results.Ok(new AssetServiceRequestOptionsResponse(
        AssetServiceRequestOptions.RequestTypes.All,
        AssetServiceRequestOptions.Priorities.All,
        AssetServiceRequestOptions.Statuses.All,
        AssetServiceRequestOptions.UpdateTypes.All,
        AssetServiceRequestOptions.FileTypes,
        AssetServiceRequestOptions.FileExtensions,
        AssetServiceRequestOptions.RelatedAssetTypes.All));

    /// <summary>
    /// Service requests, newest first. With assetRegistrationId, one asset's requests
    /// (Recent Service Requests is the first page with pageSize=3).
    /// </summary>
    private static async Task<IResult> GetRequestsAsync(
        IAssetServiceRequestRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetRegistrationId = null,
        string? status = null,
        string? priority = null,
        string? requestType = null,
        string? assignedToUserId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetServiceRequestRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetServiceRequestRepository.MaxPageSize ? AssetServiceRequestRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetServiceRequestQuery(search, assetRegistrationId, status, priority, requestType, assignedToUserId,
                ContractText.DateOnly(fromDate), ContractText.DateOnly(toDate), page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetServiceRequestListResponse(
            result.Items.Select(AssetServiceRequestListItemResponse.FromEntity).ToList(),
            result.TotalCount,
            result.StatusCounts,
            page,
            pageSize));
    }

    private static async Task<IResult> GetRequestAsync(
        string id,
        IAssetServiceRequestRepository repository,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        return request is null ? RequestNotFound(id) : Results.Ok(AssetServiceRequestResponse.FromEntity(request));
    }

    /// <summary>Submit Service Request (status Open), or Save as Draft with ?saveAsDraft=true.</summary>
    private static async Task<IResult> CreateRequestAsync(
        SaveAssetServiceRequestRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetServiceRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        IMaintenanceLookupRepository maintenanceRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var isDraft = saveAsDraft ?? false;
        var currentUser = currentUserAccessor.GetCurrentUser();
        var request = new AssetServiceRequest
        {
            Status = isDraft ? AssetServiceRequestRepository.DraftStatus : AssetServiceRequestRepository.OpenStatus,
            CreatedBy = ContractText.FirstNonEmpty(body.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(body.ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(body.TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        var error = await ApplyFormAsync(request, body, isDraft, currentUser, repository, assetRepository, maintenanceRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var year = (request.RequestedDate ?? DateTime.UtcNow).Year;
        var nextSequence = await sequenceGenerator.GetNextValueAsync($"{SequencePrefix}{year}", cancellationToken);
        request.RequestNumber = $"{RequestNumberPrefix}{year}-{nextSequence:D3}";

        if (!isDraft)
        {
            AddCreatedUpdate(request, currentUser);
        }

        await repository.CreateAsync(request, cancellationToken);
        await eventLogger.LogAsync(EntityType, request.Id, Describe(request), EventAction.Created, cancellationToken);

        return Results.Created($"/api/service-requests/{request.Id}", AssetServiceRequestResponse.FromEntity(request));
    }

    /// <summary>
    /// Edit: replaces the form's values. Attachments are kept when none are sent (null). The asset can only be
    /// changed on a draft; a closed or cancelled request cannot be edited.
    /// </summary>
    private static async Task<IResult> UpdateRequestAsync(
        string id,
        SaveAssetServiceRequestRequest body,
        [FromQuery] bool? saveAsDraft,
        IAssetServiceRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        IMaintenanceLookupRepository maintenanceRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status is AssetServiceRequestRepository.ClosedStatus or AssetServiceRequestRepository.CancelledStatus)
        {
            return Locked(request);
        }

        var isDraft = saveAsDraft == true && request.Status == AssetServiceRequestRepository.DraftStatus;
        if (request.Status != AssetServiceRequestRepository.DraftStatus
            && !string.Equals(ContractText.TrimOrNull(body.AssetRegistrationId), request.Asset.AssetRegistrationId, StringComparison.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssetRegistrationId"] = ["The asset of a submitted request cannot be changed"]
            });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var error = await ApplyFormAsync(request, body, isDraft, currentUser, repository, assetRepository, maintenanceRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return await SaveAsync(request, $"{Describe(request)} edited", repository, currentUser, eventLogger, cancellationToken);
    }

    /// <summary>Submits a draft: checks the required fields and opens the request.</summary>
    private static async Task<IResult> SubmitRequestAsync(
        string id,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status != AssetServiceRequestRepository.DraftStatus)
        {
            return Results.Conflict(new { message = $"Service request {request.RequestNumber} is already submitted." });
        }

        var errors = request.Validate(saveAsDraft: false);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        request.Status = AssetServiceRequestRepository.OpenStatus;
        AddCreatedUpdate(request, currentUser);

        return await SaveAsync(request, $"{Describe(request)} submitted", repository, currentUser, eventLogger, cancellationToken);
    }

    /// <summary>Deletes a draft. A submitted request is kept as a record; cancel it with a status update instead.</summary>
    private static async Task<IResult> DeleteRequestAsync(
        string id,
        IAssetServiceRequestRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status != AssetServiceRequestRepository.DraftStatus)
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

    /// <summary>The Updates tab, newest first; type is one of the update types ("All Updates" when empty).</summary>
    private static async Task<IResult> GetUpdatesAsync(
        string id,
        IAssetServiceRequestRepository repository,
        CancellationToken cancellationToken,
        string? type = null)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        var updates = request.Updates.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            var wanted = AssetServiceRequestOptions.UpdateTypes.Normalize(type);
            if (wanted is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Type"] = [$"Type must be one of: {string.Join(", ", AssetServiceRequestOptions.UpdateTypes.All)}"]
                });
            }

            updates = updates.Where(update => update.UpdateType == wanted);
        }

        var items = updates.OrderByDescending(update => update.CreatedAt).ToList();
        return Results.Ok(new AssetServiceRequestUpdatesResponse(items, items.Count));
    }

    /// <summary>
    /// Adds a comment and/or changes the status. Going In Progress sets Actual Start, and Closed sets Actual End,
    /// when they are not set yet. A cancelled request takes no updates; a draft must be submitted first.
    /// </summary>
    private static async Task<IResult> AddUpdateAsync(
        string id,
        AddAssetServiceRequestUpdateRequest body,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        if (request.Status is AssetServiceRequestRepository.DraftStatus or AssetServiceRequestRepository.CancelledStatus)
        {
            return Results.Conflict(new { message = $"Service request {request.RequestNumber} is {request.Status} and takes no updates." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var attachments = (body.Attachments ?? []).Select(attachment => attachment.ToEntity(currentUser?.UserName)).ToList();

        var errors = body.Validate(attachments);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var newStatus = AssetServiceRequestOptions.UpdateStatuses.Normalize(body.Status);
        var statusChanged = newStatus is not null && newStatus != request.Status;
        if (statusChanged)
        {
            SetStatus(request, newStatus!);
        }

        AddUpdate(
            request,
            statusChanged ? AssetServiceRequestOptions.StatusChangeUpdate : AssetServiceRequestOptions.CommentUpdate,
            statusChanged ? "Updated Status" : "Added Comment",
            ContractText.TrimOrNull(body.Message),
            statusChanged ? newStatus : null,
            currentUser,
            ContractText.TrimOrNull(body.RecommendedAction),
            attachments);

        return await SaveAsync(
            request,
            statusChanged ? $"{Describe(request)} - status {newStatus}" : $"{Describe(request)} - comment added",
            repository,
            currentUser,
            eventLogger,
            cancellationToken);
    }

    /// <summary>The Attachments tab: search (file name or description), file type, uploaded by and upload date range.</summary>
    private static async Task<IResult> GetAttachmentsAsync(
        string id,
        IAssetServiceRequestRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? fileType = null,
        string? uploadedBy = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetServiceRequestRepository.DefaultPageSize)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        var attachments = request.Attachments.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            attachments = attachments.Where(attachment =>
                attachment.FileName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || attachment.Description?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            attachments = attachments.Where(attachment => string.Equals(attachment.FileType, fileType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(uploadedBy))
        {
            attachments = attachments.Where(attachment => string.Equals(attachment.UploadedBy, uploadedBy.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (fromDate is not null)
        {
            attachments = attachments.Where(attachment => attachment.UploadedAt >= fromDate.Value.Date);
        }

        if (toDate is not null)
        {
            attachments = attachments.Where(attachment => attachment.UploadedAt < toDate.Value.Date.AddDays(1));
        }

        var filtered = attachments.OrderByDescending(attachment => attachment.UploadedAt).ToList();
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetServiceRequestRepository.MaxPageSize ? AssetServiceRequestRepository.DefaultPageSize : pageSize;

        return Results.Ok(new AssetServiceRequestAttachmentsResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            filtered.Count,
            request.Attachments.Select(attachment => attachment.UploadedBy).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            page,
            pageSize));
    }

    private static async Task<IResult> AddAttachmentsAsync(
        string id,
        AddAssetServiceRequestAttachmentsRequest body,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var added = (body.Attachments ?? []).Select(attachment => attachment.ToEntity(currentUser?.UserName)).ToList();
        if (added.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Attachments"] = ["Add at least one file"]
            });
        }

        var errors = new Dictionary<string, string[]>();
        AssetServiceRequestValidationExtensions.ValidateAttachments(errors, "Attachments", added, AssetServiceRequestValidationExtensions.MaxAttachments);
        if (request.Attachments.Count + added.Count > AssetServiceRequestValidationExtensions.MaxAttachments)
        {
            errors["Attachments"] = [$"A request can have at most {AssetServiceRequestValidationExtensions.MaxAttachments} attachments"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        request.Attachments.AddRange(added);
        return await SaveAsync(request, $"{Describe(request)} - {added.Count} attachments added", repository, currentUser, eventLogger, cancellationToken);
    }

    private static Task<IResult> DeleteAttachmentAsync(
        string id,
        string attachmentId,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken) =>
        RemoveAttachmentsAsync(id, [attachmentId], repository, currentUserAccessor, eventLogger, cancellationToken);

    private static Task<IResult> DeleteAttachmentsAsync(
        string id,
        DeleteAssetServiceRequestAttachmentsRequest body,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken) =>
        RemoveAttachmentsAsync(id, ContractText.CleanList(body.AttachmentIds, StringComparer.Ordinal), repository, currentUserAccessor, eventLogger, cancellationToken);

    private static async Task<IResult> RemoveAttachmentsAsync(
        string id,
        IReadOnlyCollection<string> attachmentIds,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var removed = request.Attachments.RemoveAll(attachment => attachmentIds.Contains(attachment.AttachmentId));
        if (removed == 0)
        {
            return Results.NotFound(new { message = "No matching attachment was found." });
        }

        return await SaveAsync(request, $"{Describe(request)} - {removed} attachments removed", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>The Related tab. Work orders are refreshed from the Maintenance service (title, status, priority…).</summary>
    private static async Task<IResult> GetRelatedAsync(
        string id,
        IAssetServiceRequestRepository repository,
        IMaintenanceLookupRepository maintenanceRepository,
        CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return RequestNotFound(id);
        }

        var current = (await maintenanceRepository.FindWorkOrdersAsync(request.WorkOrders.Select(workOrder => workOrder.WorkOrderId).ToList(), cancellationToken))
            .GroupBy(workOrder => workOrder.WorkOrderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var workOrder in request.WorkOrders)
        {
            if (current.TryGetValue(workOrder.WorkOrderId, out var latest))
            {
                CopyWorkOrder(workOrder, latest);
            }
        }

        var documents = request.Attachments
            .Select(attachment => new AssetServiceRequestDocumentResponse(
                attachment.AttachmentId, attachment.FileName, attachment.FileType, attachment.FileUrl, attachment.UploadedAt, attachment.UploadedBy, "Service Request"))
            .Concat(request.Documents.Select(document => new AssetServiceRequestDocumentResponse(
                document.DocumentId, document.DocumentName ?? document.FileName, document.FileType, document.FileUrl, document.UploadedAt, document.UploadedBy, "Asset")))
            .ToList();

        return Results.Ok(new AssetServiceRequestRelatedResponse(
            request.WorkOrders,
            request.RelatedAssets,
            request.SpareParts,
            documents,
            AssetServiceRequestCounts.FromEntity(request)));
    }

    /// <summary>Link Work Order: links an existing work order by its number.</summary>
    private static async Task<IResult> LinkWorkOrderAsync(
        string id,
        LinkAssetServiceRequestWorkOrderRequest body,
        IAssetServiceRequestRepository repository,
        IMaintenanceLookupRepository maintenanceRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var error = await AddWorkOrderAsync(request, body.WorkOrderId, currentUser, maintenanceRepository, cancellationToken);
        return error ?? await SaveAsync(request, $"{Describe(request)} - work order {body.WorkOrderId!.Trim()} linked", repository, currentUser, eventLogger, cancellationToken);
    }

    private static async Task<IResult> UnlinkWorkOrderAsync(
        string id,
        string workOrderId,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        return request.WorkOrders.RemoveAll(workOrder => string.Equals(workOrder.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase)) == 0
            ? Results.NotFound(new { message = $"Work order '{workOrderId}' is not linked to this request." })
            : await SaveAsync(request, $"{Describe(request)} - work order {workOrderId} unlinked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>
    /// Convert to Work Order: links the work order just created in the Maintenance service as the request's work order,
    /// records it on the Updates tab, and closes the request unless CloseRequest is false.
    /// </summary>
    private static async Task<IResult> ConvertToWorkOrderAsync(
        string id,
        ConvertAssetServiceRequestRequest body,
        IAssetServiceRequestRepository repository,
        IMaintenanceLookupRepository maintenanceRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        if (request.Status == AssetServiceRequestRepository.DraftStatus)
        {
            return Results.Conflict(new { message = $"Submit {request.RequestNumber} before converting it to a work order." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var workOrderId = ContractText.TrimOrNull(body.WorkOrderId);
        var alreadyLinked = request.WorkOrders.FindIndex(workOrder => string.Equals(workOrder.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase));

        if (alreadyLinked < 0)
        {
            var error = await AddWorkOrderAsync(request, workOrderId, currentUser, maintenanceRepository, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            alreadyLinked = request.WorkOrders.Count - 1;
        }

        // The converted work order becomes the request's work order (shown in Work Details).
        var converted = request.WorkOrders[alreadyLinked];
        request.WorkOrders.RemoveAt(alreadyLinked);
        request.WorkOrders.Insert(0, converted);

        if (body.CloseRequest)
        {
            SetStatus(request, AssetServiceRequestRepository.ClosedStatus);
        }

        AddUpdate(
            request,
            AssetServiceRequestOptions.WorkOrderUpdate,
            body.CloseRequest ? "Updated Status" : "Linked Work Order",
            ContractText.TrimOrNull(body.Message) ?? $"Work order {converted.WorkOrderId} created and linked to this service request.",
            body.CloseRequest ? AssetServiceRequestRepository.ClosedStatus : null,
            currentUser);

        return await SaveAsync(request, $"{Describe(request)} - converted to work order {converted.WorkOrderId}", repository, currentUser, eventLogger, cancellationToken);
    }

    /// <summary>Link Asset/Component: another asset, or a component (of this asset or another).</summary>
    private static async Task<IResult> LinkRelatedAssetAsync(
        string id,
        LinkAssetServiceRequestRelatedAssetRequest body,
        IAssetServiceRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var type = AssetServiceRequestOptions.RelatedAssetTypes.Normalize(body.Type);
        var referenceId = ContractText.TrimOrNull(body.ReferenceId);
        var relationship = ContractText.TrimOrNull(body.Relationship);
        if (type is null || referenceId is null || relationship?.Length > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [type is null ? "Type" : referenceId is null ? "ReferenceId" : "Relationship"] =
                    [type is null ? "Type must be Asset or Component" : referenceId is null ? "ReferenceId is required" : "Relationship cannot be longer than 100 characters"]
            });
        }

        if (request.RelatedAssets.Any(related => related.Type == type && related.ReferenceId == referenceId))
        {
            return Results.Conflict(new { message = $"This {type.ToLowerInvariant()} is already linked." });
        }

        if (request.RelatedAssets.Count >= AssetServiceRequestValidationExtensions.MaxLinks)
        {
            return TooManyLinks("RelatedAssets");
        }

        AssetServiceRequestRelatedAsset related;
        if (type == AssetServiceRequestOptions.AssetRelation)
        {
            if (referenceId == request.Asset.AssetRegistrationId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ReferenceId"] = ["This is the request's own asset"]
                });
            }

            var asset = await assetRepository.GetByAssetRegistrationIdAsync(referenceId, cancellationToken);
            if (asset is null)
            {
                return NotFoundProblem("ReferenceId", $"Asset '{referenceId}' was not found");
            }

            related = new AssetServiceRequestRelatedAsset
            {
                Type = type,
                ReferenceId = asset.AssetRegistrationId,
                Number = asset.BasicInformation.AssetTag,
                Name = asset.BasicInformation.AssetName,
                Relationship = relationship ?? "Related asset"
            };
        }
        else
        {
            var component = await componentRepository.GetByIdAsync(referenceId, cancellationToken);
            if (component is null || component.IsDeleted)
            {
                return NotFoundProblem("ReferenceId", $"Component '{referenceId}' was not found");
            }

            var owner = component.AssetRegistrationId == request.Asset.AssetRegistrationId
                ? request.Asset.AssetTag ?? component.AssetRegistrationId
                : (await assetRepository.GetByAssetRegistrationIdAsync(component.AssetRegistrationId, cancellationToken))?.BasicInformation.AssetTag
                    ?? component.AssetRegistrationId;

            related = new AssetServiceRequestRelatedAsset
            {
                Type = type,
                ReferenceId = component.Id,
                Number = component.ComponentTag,
                Name = component.ComponentName,
                Relationship = relationship ?? $"Part of {owner}"
            };
        }

        request.RelatedAssets.Add(related);
        return await SaveAsync(request, $"{Describe(request)} - {type.ToLowerInvariant()} {related.Number} linked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    private static async Task<IResult> UnlinkRelatedAssetAsync(
        string id,
        string referenceId,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        return request.RelatedAssets.RemoveAll(related => related.ReferenceId == referenceId) == 0
            ? Results.NotFound(new { message = $"'{referenceId}' is not linked to this request." })
            : await SaveAsync(request, $"{Describe(request)} - related asset {referenceId} unlinked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>Link Spare Parts: a part from the Maintenance service, the quantity used, and the work order it was used on.</summary>
    private static async Task<IResult> LinkSparePartAsync(
        string id,
        LinkAssetServiceRequestSparePartRequest body,
        IAssetServiceRequestRepository repository,
        IMaintenanceLookupRepository maintenanceRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var errors = new Dictionary<string, string[]>();
        var partId = ContractText.TrimOrNull(body.PartId);
        var workOrderId = ContractText.TrimOrNull(body.WorkOrderId);

        if (partId is null)
        {
            errors["PartId"] = ["PartId is required"];
        }

        if (body.QuantityUsed is < 1 or > 100_000)
        {
            errors["QuantityUsed"] = ["QuantityUsed must be at least 1"];
        }

        if (workOrderId is not null && request.WorkOrders.All(workOrder => !string.Equals(workOrder.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase)))
        {
            errors["WorkOrderId"] = [$"Work order '{workOrderId}' is not linked to this request; link it first"];
        }

        if (request.SpareParts.Count >= AssetServiceRequestValidationExtensions.MaxLinks)
        {
            errors["SpareParts"] = [$"At most {AssetServiceRequestValidationExtensions.MaxLinks} can be linked"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var part = (await maintenanceRepository.FindSparePartsAsync([partId!], cancellationToken)).FirstOrDefault();
        if (part is null)
        {
            return NotFoundProblem("PartId", $"Spare part '{partId}' was not found");
        }

        if (request.SpareParts.Any(used => string.Equals(used.PartId, part.PartId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(used.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = $"Spare part {part.PartId} is already linked for this work order." });
        }

        request.SpareParts.Add(new AssetServiceRequestSparePart
        {
            PartId = part.PartId,
            PartName = part.PartName,
            QuantityUsed = body.QuantityUsed,
            UsedDate = ContractText.DateOnly(body.UsedDate) ?? ContractText.DateOnly(DateTime.UtcNow),
            WorkOrderId = workOrderId
        });

        return await SaveAsync(request, $"{Describe(request)} - spare part {part.PartId} x{body.QuantityUsed} linked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>Removes a linked spare part (every line for that part number).</summary>
    private static async Task<IResult> UnlinkSparePartAsync(
        string id,
        string partId,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        return request.SpareParts.RemoveAll(part => string.Equals(part.PartId, partId, StringComparison.OrdinalIgnoreCase)) == 0
            ? Results.NotFound(new { message = $"Spare part '{partId}' is not linked to this request." })
            : await SaveAsync(request, $"{Describe(request)} - spare part {partId} unlinked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>Link Document: an asset document (from the asset's Documents).</summary>
    private static async Task<IResult> LinkDocumentAsync(
        string id,
        LinkAssetServiceRequestDocumentRequest body,
        IAssetServiceRequestRepository repository,
        IAssetDocumentRepository documentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        var documentId = ContractText.TrimOrNull(body.DocumentId);
        if (documentId is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["DocumentId"] = ["DocumentId is required"]
            });
        }

        if (request.Documents.Any(document => document.DocumentId == documentId))
        {
            return Results.Conflict(new { message = "This document is already linked." });
        }

        if (request.Documents.Count >= AssetServiceRequestValidationExtensions.MaxLinks)
        {
            return TooManyLinks("Documents");
        }

        var record = await documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (record is null || record.IsDeleted)
        {
            return NotFoundProblem("DocumentId", $"Document '{documentId}' was not found");
        }

        request.Documents.Add(new AssetServiceRequestDocument
        {
            DocumentId = record.Id,
            DocumentName = record.DocumentName,
            FileName = record.FileName,
            FileUrl = record.FileUrl,
            FileType = AssetServiceRequestOptions.FileTypeOf(record.FileName) ?? record.ContentType,
            UploadedBy = record.CreatedBy,
            UploadedAt = record.CreatedAt
        });

        return await SaveAsync(request, $"{Describe(request)} - document {record.DocumentName} linked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    private static async Task<IResult> UnlinkDocumentAsync(
        string id,
        string documentId,
        IAssetServiceRequestRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (request, problem) = await FindOpenAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return problem!;
        }

        return request.Documents.RemoveAll(document => document.DocumentId == documentId) == 0
            ? Results.NotFound(new { message = $"Document '{documentId}' is not linked to this request." })
            : await SaveAsync(request, $"{Describe(request)} - document {documentId} unlinked", repository, currentUserAccessor.GetCurrentUser(), eventLogger, cancellationToken);
    }

    /// <summary>
    /// Copies the form onto the request and checks it. Blank Location, Department, Requested By and Requested Date
    /// default to the asset's location and department, the signed-in user and now.
    /// </summary>
    private static async Task<IResult?> ApplyFormAsync(
        AssetServiceRequest request,
        SaveAssetServiceRequestRequest body,
        bool isDraft,
        CurrentUser? currentUser,
        IAssetServiceRequestRepository repository,
        IAssetRegistrationRepository assetRepository,
        IMaintenanceLookupRepository maintenanceRepository,
        CancellationToken cancellationToken)
    {
        var assetId = ContractText.TrimOrNull(body.AssetRegistrationId);
        AssetRegistration? asset = null;
        if (assetId is not null && assetId != request.Asset.AssetRegistrationId)
        {
            asset = await assetRepository.GetByAssetRegistrationIdAsync(assetId, cancellationToken);
            if (asset is null)
            {
                return NotFoundProblem("AssetRegistrationId", $"Asset '{assetId}' was not found");
            }

            request.Asset = ToRequestAsset(asset);
        }
        else if (assetId is null)
        {
            request.Asset = new AssetServiceRequestAsset();
        }

        var userName = currentUser?.UserName;
        request.Title = body.Title?.Trim() ?? string.Empty;
        request.RequestType = AssetServiceRequestOptions.RequestTypes.NormalizeOrKeep(body.RequestType);
        request.Priority = AssetServiceRequestOptions.Priorities.NormalizeOrKeep(body.Priority);
        request.RequestedDate = Utc(body.RequestedDate) ?? request.RequestedDate ?? DateTime.UtcNow;
        request.TargetDate = ContractText.DateOnly(body.TargetDate);
        request.Description = ContractText.TrimOrNull(body.Description);
        request.Location = body.Location?.ToEntity() ?? DefaultLocation(asset, request);
        request.Department = body.Department?.ToEntity() ?? DefaultDepartment(asset, request);
        request.RequestedBy = body.RequestedBy?.ToEntity()
            ?? (currentUser is null ? null : new AssetServiceRequestPerson { UserId = currentUser.UserId ?? currentUser.Id, Name = currentUser.UserName });
        request.AssignedTo = body.AssignedTo?.ToEntity();
        request.EstimatedDowntimeHours = body.EstimatedDowntimeHours;
        request.PlannedStart = Utc(body.PlannedStart);
        request.PlannedEnd = Utc(body.PlannedEnd);
        request.ActualStart = Utc(body.ActualStart) ?? request.ActualStart;
        request.ActualEnd = Utc(body.ActualEnd) ?? request.ActualEnd;
        request.RelatedServiceRequest = ContractText.TrimOrNull(body.RelatedServiceRequest);
        request.Tags = ContractText.CleanList(body.Tags, StringComparer.OrdinalIgnoreCase);
        request.ReferenceNumber = ContractText.TrimOrNull(body.ReferenceNumber);
        if (body.Attachments is not null)
        {
            request.Attachments = body.Attachments.Select(attachment => attachment.ToEntity(userName)).ToList();
        }

        var errors = request.Validate(isDraft);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (request.RelatedServiceRequest is { } relatedNumber)
        {
            var related = await repository.GetByRequestNumberAsync(relatedNumber, cancellationToken);
            if (related is null || related.Id == request.Id)
            {
                return NotFoundProblem("RelatedServiceRequest", related is null ? $"Service request '{relatedNumber}' was not found" : "A request cannot be related to itself");
            }

            request.RelatedServiceRequest = related.RequestNumber;
        }

        var workOrderId = ContractText.TrimOrNull(body.RelatedWorkOrderId);
        if (workOrderId is not null && request.WorkOrders.All(workOrder => !string.Equals(workOrder.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase)))
        {
            return await AddWorkOrderAsync(request, workOrderId, currentUser, maintenanceRepository, cancellationToken);
        }

        return null;
    }

    /// <summary>Looks the work order up in the Maintenance service and adds it to the request's links.</summary>
    private static async Task<IResult?> AddWorkOrderAsync(
        AssetServiceRequest request,
        string? workOrderId,
        CurrentUser? currentUser,
        IMaintenanceLookupRepository maintenanceRepository,
        CancellationToken cancellationToken)
    {
        workOrderId = ContractText.TrimOrNull(workOrderId);
        if (workOrderId is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["WorkOrderId"] = ["WorkOrderId is required"]
            });
        }

        if (request.WorkOrders.Any(workOrder => string.Equals(workOrder.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = $"Work order {workOrderId} is already linked." });
        }

        if (request.WorkOrders.Count >= AssetServiceRequestValidationExtensions.MaxLinks)
        {
            return TooManyLinks("WorkOrders");
        }

        var found = (await maintenanceRepository.FindWorkOrdersAsync([workOrderId], cancellationToken)).FirstOrDefault();
        if (found is null)
        {
            return NotFoundProblem("WorkOrderId", $"Work order '{workOrderId}' was not found");
        }

        var link = new AssetServiceRequestWorkOrder
        {
            WorkOrderId = found.WorkOrderId,
            LinkedBy = currentUser?.UserName,
            LinkedAt = DateTime.UtcNow
        };
        CopyWorkOrder(link, found);
        request.WorkOrders.Add(link);
        return null;
    }

    private static void CopyWorkOrder(AssetServiceRequestWorkOrder link, MaintenanceWorkOrderLookup workOrder)
    {
        link.Title = workOrder.Title;
        link.WorkType = workOrder.WorkType;
        link.Status = workOrder.Status;
        link.Priority = workOrder.Priority;
        link.PlannedStart = workOrder.ScheduledDate;
        link.AssignedTo = workOrder.AssignedTechnician;
    }

    /// <summary>Changes the status; the first move to In Progress sets Actual Start and closing sets Actual End.</summary>
    private static void SetStatus(AssetServiceRequest request, string status)
    {
        request.Status = status;
        if (status == AssetServiceRequestRepository.InProgressStatus)
        {
            request.ActualStart ??= DateTime.UtcNow;
        }
        else if (status == AssetServiceRequestRepository.ClosedStatus)
        {
            request.ActualStart ??= DateTime.UtcNow;
            request.ActualEnd ??= DateTime.UtcNow;
        }
    }

    private static void AddCreatedUpdate(AssetServiceRequest request, CurrentUser? currentUser)
    {
        request.SubmittedAt = DateTime.UtcNow;
        AddUpdate(request, AssetServiceRequestOptions.CreatedUpdate, "Created the Service Request", request.Description, request.Status, currentUser);

        // The timeline shows the requester, who may be raising it on someone's behalf.
        var created = request.Updates[^1];
        created.UserId = request.RequestedBy?.UserId ?? created.UserId;
        created.UserName = request.RequestedBy?.Name ?? created.UserName;
    }

    private static void AddUpdate(
        AssetServiceRequest request,
        string updateType,
        string action,
        string? message,
        string? status,
        CurrentUser? currentUser,
        string? recommendedAction = null,
        List<AssetServiceRequestAttachment>? attachments = null)
    {
        request.Updates.Add(new AssetServiceRequestUpdate
        {
            UpdateId = Guid.NewGuid().ToString("N"),
            UpdateType = updateType,
            Action = action,
            Message = message,
            Status = status,
            RecommendedAction = recommendedAction,
            Attachments = attachments ?? [],
            UserId = currentUser?.UserId ?? currentUser?.Id,
            UserName = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static AssetServiceRequestAsset ToRequestAsset(AssetRegistration asset) => new()
    {
        AssetRegistrationId = asset.AssetRegistrationId,
        AssetName = asset.BasicInformation.AssetName,
        AssetTag = asset.BasicInformation.AssetTag,
        Category = asset.Classification.Category,
        Location = asset.LocationAssignment.Location.LocationPath,
        Department = asset.LocationAssignment.Assignment.CustodianDepartment ?? asset.Classification.Department,
        Status = asset.BasicInformation.Status,
        ImageUrl = asset.BasicInformation.Images.FirstOrDefault()
    };

    /// <summary>The asset's location (its lowest level), when the form leaves Location empty.</summary>
    private static AssetServiceRequestReference? DefaultLocation(AssetRegistration? asset, AssetServiceRequest request)
    {
        if (asset is null)
        {
            return request.Asset.Location is { } path ? new AssetServiceRequestReference { Name = path } : null;
        }

        var location = asset.LocationAssignment.Location;
        return location.LocationPath is null
            ? null
            : new AssetServiceRequestReference
            {
                Id = location.RoomId ?? location.ZoneId ?? location.FloorId ?? location.BuildingId ?? location.SiteId,
                Name = location.LocationPath
            };
    }

    private static AssetServiceRequestReference? DefaultDepartment(AssetRegistration? asset, AssetServiceRequest request)
    {
        var name = asset is null
            ? request.Asset.Department
            : asset.LocationAssignment.Assignment.CustodianDepartment ?? asset.Classification.Department;
        return name is null ? null : new AssetServiceRequestReference { Name = name };
    }

    /// <summary>Dates with a time are kept as sent; one without a time zone is read as UTC so it is not shifted.</summary>
    private static DateTime? Utc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        var date => date.Value.ToUniversalTime()
    };

    private static async Task<IResult> SaveAsync(
        AssetServiceRequest request,
        string change,
        IAssetServiceRequestRepository repository,
        CurrentUser? currentUser,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        request.UpdatedBy = currentUser?.UserName;
        request.UpdatedAt = DateTime.UtcNow;
        if (!await repository.UpdateAsync(request.Id, request, cancellationToken))
        {
            return RequestNotFound(request.Id);
        }

        await eventLogger.LogAsync(EntityType, request.Id, change, EventAction.Updated, cancellationToken);
        return Results.Ok(AssetServiceRequestResponse.FromEntity(request));
    }

    private static async Task<AssetServiceRequest?> FindAsync(string id, IAssetServiceRequestRepository repository, CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(id, cancellationToken);
        return request is null || request.IsDeleted ? null : request;
    }

    /// <summary>Attachments and links can be changed on any request that is not cancelled.</summary>
    private static async Task<(AssetServiceRequest? Request, IResult? Problem)> FindOpenAsync(string id, IAssetServiceRequestRepository repository, CancellationToken cancellationToken)
    {
        var request = await FindAsync(id, repository, cancellationToken);
        if (request is null)
        {
            return (null, RequestNotFound(id));
        }

        return request.Status == AssetServiceRequestRepository.CancelledStatus ? (null, Locked(request)) : (request, null);
    }

    private static string Describe(AssetServiceRequest request) => $"{request.RequestNumber} - {request.Title}";

    private static IResult Locked(AssetServiceRequest request) =>
        Results.Conflict(new { message = $"Service request {request.RequestNumber} is {request.Status} and can no longer be changed." });

    private static IResult TooManyLinks(string field) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [$"At most {AssetServiceRequestValidationExtensions.MaxLinks} can be linked"]
        });

    private static IResult NotFoundProblem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
        });

    private static IResult RequestNotFound(string id) =>
        Results.NotFound(new { message = $"Service request '{id}' was not found." });
}
