using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Telemetry;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Tracking & IoT → Tag Management: the stat cards, the Tag IDs / Assignments / Audit Trail tabs, the tag side panel and
/// the Register Tag ID form. Tags are addressed by tag id (e.g. BLE-002951). Every change is written to the Audit Trail.
/// </summary>
public static class TrackingTagEndpoints
{
    private const string EntityType = "TrackingTag";
    private const int DefaultPageSize = 10;

    public static RouteGroupBuilder MapTrackingTagEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tag-ids").WithTags("Tag Management");

        // Literal segments take precedence over /{tagId}, so these are not read as tag ids.
        group.MapGet("/summary", GetSummaryAsync).WithName("GetTagSummary");
        group.MapGet("/options", GetOptions).WithName("GetTagOptions");
        group.MapGet("/assignments", GetAssignmentsAsync).WithName("GetTagAssignments");
        group.MapGet("/available", GetAvailableAsync).WithName("GetAvailableTags");
        group.MapGet("/audit-trail", GetAuditTrailAsync).WithName("GetTagAuditTrail");
        group.MapGet("/audit-trail/options", GetAuditOptionsAsync).WithName("GetTagAuditOptions");
        group.MapGet("/asset-movement", GetAssetMovementAsync).WithName("GetTagAssetMovement");
        group.MapGet("/asset-movement/{assetId}", GetAssetMovementByAssetAsync).WithName("GetTagAssetMovementByAsset");

        group.MapGet("", GetTagsAsync).WithName("GetTrackingTags");
        group.MapGet("/{tagId}", GetTagAsync).WithName("GetTrackingTagById");
        group.MapGet("/{tagId}/history", GetHistoryAsync).WithName("GetTrackingTagHistory");

        group.MapPost("", RegisterTagAsync).WithName("RegisterTrackingTag");
        group.MapPut("/{tagId}", UpdateTagAsync).WithName("UpdateTrackingTag");
        group.MapPost("/{tagId}/assign", AssignTagAsync).WithName("AssignTrackingTag");
        group.MapPost("/{tagId}/unassign", UnassignTagAsync).WithName("UnassignTrackingTag");
        group.MapPost("/{tagId}/deactivate", DeactivateTagAsync).WithName("DeactivateTrackingTag");
        group.MapPost("/{tagId}/reactivate", ReactivateTagAsync).WithName("ReactivateTrackingTag");
        group.MapPost("/{tagId}/reads", RecordReadAsync).WithName("RecordTrackingTagRead");
        group.MapDelete("/{tagId}", DeleteTagAsync).WithName("DeleteTrackingTag");

        return group;
    }

    // ------------------------------------------------------------ Reads

    /// <summary>Total / Assigned / Available / Inactive cards with the change vs a month ago, and the failing-validation badge.</summary>
    private static async Task<IResult> GetSummaryAsync(
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        var all = await tags.GetActiveAsync(cancellationToken);
        await ReleaseTagsOfDeletedAssetsAsync(all, tags, events, assets, cancellationToken);
        var validations = Validate(all);

        var monthAgo = DateTime.UtcNow.AddMonths(-1);
        var statusesThen = await events.GetStatusesAtAsync(monthAgo, cancellationToken);

        // A tag's status a month ago is the status after its last event before then; tags from before the
        // audit trail existed count with their current status if they were registered by then.
        var then = statusesThen.Values.ToList();
        then.AddRange(all
            .Where(tag => tag.CreatedAt < monthAgo && !statusesThen.ContainsKey(tag.TagId))
            .Select(tag => tag.Status));
        then.RemoveAll(status => status is null);

        TagCountResponse Card(Func<string?, bool> matches)
        {
            var now = all.Count(tag => matches(tag.Status));
            var before = then.Count(matches);
            return new TagCountResponse(now, before, DashboardPeriod.ChangePercent(now, before));
        }

        return Results.Ok(new TagSummaryResponse(
            Card(_ => true),
            Card(status => status == TagStatus.Assigned),
            Card(status => status == TagStatus.Available),
            Card(status => status == TagStatus.Inactive),
            validations.Values.Count(validation => !validation.IsValid),
            TagTechnology.All.ToDictionary(technology => technology, technology => all.Count(tag => tag.Technology == technology))));
    }

    private static IResult GetOptions()
    {
        return Results.Ok(new TagOptionsResponse(
            TagTechnology.All.Select(TagTechnologyOptionResponse.FromTechnology).ToList(),
            TagTechnology.TagTypes.Values.SelectMany(types => types).Distinct().ToList(),
            TagStatus.All,
            TagValidation.All));
    }

    /// <summary>The Tag IDs tab.</summary>
    private static Task<IResult> GetTagsAsync(
        [AsParameters] TagListQuery query,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        return ListAsync(query, null, tags, events, assets, cancellationToken);
    }

    /// <summary>The Assignments tab: tags linked to an asset.</summary>
    private static Task<IResult> GetAssignmentsAsync(
        [AsParameters] TagListQuery query,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        return ListAsync(query, TagStatus.Assigned, tags, events, assets, cancellationToken);
    }

    /// <summary>Tags in stock, not linked to any asset.</summary>
    private static Task<IResult> GetAvailableAsync(
        [AsParameters] TagListQuery query,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        return ListAsync(query, TagStatus.Available, tags, events, assets, cancellationToken);
    }

    private static async Task<IResult> ListAsync(
        TagListQuery query,
        string? fixedStatus,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var all = await tags.GetActiveAsync(cancellationToken);
        await ReleaseTagsOfDeletedAssetsAsync(all, tags, events, assets, cancellationToken);
        var validations = Validate(all);

        var technology = ContractText.Canonical(query.Technology, TagTechnology.All);
        var status = fixedStatus ?? ContractText.Canonical(query.Status, TagStatus.All);
        var validation = ContractText.Canonical(query.Validation, TagValidation.All);
        var tagType = ContractText.TrimOrNull(query.TagType);
        var siteId = ContractText.TrimOrNull(query.SiteId);
        var search = ContractText.TrimOrNull(query.Search);

        var matching = all.Where(tag =>
                (technology is null || tag.Technology == technology)
                && (status is null || tag.Status == status)
                && (validation is null || validations[tag.Id].Result == validation)
                && (tagType is null || string.Equals(tag.TagType, tagType, StringComparison.OrdinalIgnoreCase))
                && (siteId is null || string.Equals(tag.SiteId, siteId, StringComparison.OrdinalIgnoreCase))
                && (search is null || Matches(tag, search)))
            .ToList();

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(tag => tag.ToListItem(validations[tag.Id]))
            .ToList();

        return Results.Ok(PagedResponse<TagListItemResponse>.Of(items, matching.Count, page, pageSize));
    }

    /// <summary>The tag side panel: identifier, linked asset, location, battery & connectivity and assignment history.</summary>
    private static async Task<IResult> GetTagAsync(
        string tagId,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        await ReleaseTagsOfDeletedAssetsAsync([tag], tags, events, assets, cancellationToken);
        var validation = await ValidateAsync(tag, tags, cancellationToken);
        var history = (await events.GetByTagIdAsync(tag.TagId, cancellationToken))
            .Where(tagEvent => TagEventType.Assignment.Contains(tagEvent.EventType))
            .ToList();

        return Results.Ok(tag.ToDetails(validation, history));
    }

    /// <summary>The History tab of the side panel: every event of the tag, newest first.</summary>
    private static async Task<IResult> GetHistoryAsync(
        string tagId,
        ITagEventRepository events,
        CancellationToken cancellationToken)
    {
        var history = await events.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        return Results.Ok(history.Select(TagEventResponse.FromEntity).ToList());
    }

    /// <summary>
    /// Asset movement / history: assets mapped to tags here, matched to NetworkTimeSeries readings. Per asset: how many
    /// gateways heard it (two gateways = 2), how many zones, how many zone-to-zone movements, and (history=true) each visit.
    /// </summary>
    /// <param name="from">Start (UTC); default 24 hours before <paramref name="to"/>.</param>
    /// <param name="to">End (UTC, exclusive); default now.</param>
    /// <param name="technology">BLE, Passive RFID, Active RFID, RFID (both), QR Code, Barcode or NFC.</param>
    /// <param name="search">Matches asset id / code / name, tag id, gateway id or zone name.</param>
    private static async Task<IResult> GetAssetMovementAsync(
        AssetMovementService service,
        CancellationToken cancellationToken,
        DateTime? from = null,
        DateTime? to = null,
        string? assetId = null,
        string? tagId = null,
        string? technology = null,
        string? search = null,
        bool history = false,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var errors = MovementErrors(from, to, page, pageSize, out var fromUtc, out var toUtc);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(await service.GetAsync(
            fromUtc, toUtc, ContractText.TrimOrNull(assetId), ContractText.TrimOrNull(tagId), ContractText.TrimOrNull(technology),
            ContractText.TrimOrNull(search),
            history, page, pageSize, cancellationToken));
    }

    /// <summary>One asset's movement with its visit-by-visit history (asset registration id or asset code).</summary>
    private static async Task<IResult> GetAssetMovementByAssetAsync(
        string assetId,
        AssetMovementService service,
        CancellationToken cancellationToken,
        DateTime? from = null,
        DateTime? to = null)
    {
        var errors = MovementErrors(from, to, 1, 1, out var fromUtc, out var toUtc);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await service.GetAsync(fromUtc, toUtc, assetId.Trim(), null, null, null, true, 1, 1, cancellationToken);
        return result.Items.Count == 0
            ? Results.NotFound(new { message = $"No tags in Tag Management are mapped to asset '{assetId}'." })
            : Results.Ok(result.Items[0]);
    }

    private static Dictionary<string, string[]> MovementErrors(
        DateTime? from, DateTime? to, int page, int pageSize, out DateTime fromUtc, out DateTime toUtc)
    {
        toUtc = to is null ? DateTime.UtcNow : ToUtc(to.Value);
        fromUtc = from is null ? toUtc.AddHours(-24) : ToUtc(from.Value);

        var errors = new Dictionary<string, string[]>();
        if (fromUtc >= toUtc) errors["to"] = ["to must be after from"];
        if (toUtc - fromUtc > TimeSpan.FromDays(31)) errors["from"] = ["The period can be at most 31 days"];
        if (page < 1) errors["page"] = ["page must be 1 or more"];
        if (pageSize is < 1 or > 200) errors["pageSize"] = ["pageSize must be between 1 and 200"];
        return errors;

        static DateTime ToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
    }

    /// <summary>The Audit Trail tab: every registration, assignment, read and status change, with who made it.</summary>
    private static async Task<IResult> GetAuditTrailAsync(
        [AsParameters] TagAuditQuery query,
        ITagEventRepository events,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var usesDates = query.From is not null || query.To is not null;
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;

        var (items, total) = await events.QueryAsync(new TagEventQuery(
            ContractText.TrimOrNull(query.Search),
            ContractText.Canonical(query.EventType, TagEventType.All),
            ContractText.Canonical(query.Technology, TagTechnology.All),
            ContractText.TrimOrNull(query.PerformedBy),
            TagId: null,
            usesDates ? query.From?.ToUniversalTime() : TagAuditPeriod.StartOf(query.Period, DateTime.UtcNow),
            usesDates ? query.To?.ToUniversalTime() : null,
            page,
            pageSize), cancellationToken);

        return Results.Ok(PagedResponse<TagEventResponse>.Of(
            items.Select(TagEventResponse.FromEntity).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetAuditOptionsAsync(
        ITagEventRepository events,
        CancellationToken cancellationToken)
    {
        return Results.Ok(new TagAuditOptionsResponse(
            TagEventType.All,
            TagTechnology.All,
            await events.GetPerformersAsync(cancellationToken),
            TagAuditPeriod.All));
    }

    // ------------------------------------------------------------ Writes

    /// <summary>The Register Tag ID form.</summary>
    private static async Task<IResult> RegisterTagAsync(
        RegisterTagRequest request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var technology = ContractText.Canonical(request.Technology, TagTechnology.All)!;
        // The tag id is typed by the user (e.g. a BLE MAC c300006eb011) and stored as entered; unique ignoring case.
        var tagId = request.TagNumber!.Trim();
        var tagNumber = tagId;
        var value = TagValueFormat.Normalize(technology, request.Value);

        if (await tags.TagIdExistsAsync(tagId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Tag ID '{tagId}' already exists." });
        }

        if (await ValueConflictAsync(tags, technology, value, null, cancellationToken) is { } valueConflict)
        {
            return valueConflict;
        }

        TagAsset? asset = null;
        if (ContractText.TrimOrNull(request.AssetCode) is { } assetCode)
        {
            asset = await assets.FindAsync(assetCode, cancellationToken);
            if (asset is null)
            {
                return UnknownAsset(assetCode);
            }
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        var tag = new TrackingTag
        {
            TagId = tagId,
            TagNumber = tagNumber,
            Technology = technology,
            TagType = ContractText.Canonical(request.TagType, TagTechnology.TagTypes[technology])!,
            Value = value,
            FirmwareVersion = ContractText.TrimOrNull(request.FirmwareVersion),
            SiteId = ContractText.TrimOrNull(request.SiteId),
            SiteName = ContractText.TrimOrNull(request.SiteName),
            LocationName = ContractText.TrimOrNull(request.LocationName),
            Status = TagStatus.Available,
            CreatedBy = currentUser?.UserName,
            CreatedAt = now,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };

        await tags.CreateAsync(tag, cancellationToken);
        await RecordAsync(events, tag, TagEventType.Registered,
            $"Registered as {technology} {tag.TagType}" + (tag.SiteName is null ? string.Empty : $" at {Place(tag)}"),
            Actor(currentUser), now, cancellationToken);

        if (asset is not null)
        {
            LinkAsset(tag, asset, Actor(currentUser), now);
            await tags.UpdateAsync(tag.Id, tag, cancellationToken);
            await RecordAsync(events, tag, TagEventType.Assigned, $"Assigned to {TagMappings.AssetDisplay(asset)}",
                Actor(currentUser), now, cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Created, cancellationToken);

        var validation = await ValidateAsync(tag, tags, cancellationToken);
        return Results.Created($"/api/tag-ids/{tag.TagId}", tag.ToDetails(validation, []));
    }

    private static async Task<IResult> UpdateTagAsync(
        string tagId,
        UpdateTagRequest request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        var errors = request.Validate(tag.Technology);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var changes = new List<string>();
        void Set(string label, string? current, string? requested, Action<string?> apply)
        {
            if (requested is not null && !string.Equals(current, requested, StringComparison.Ordinal))
            {
                apply(requested);
                changes.Add($"{label} {current ?? "—"} → {requested}");
            }
        }

        var value = TagValueFormat.Normalize(tag.Technology, request.Value);
        if (value is not null
            && await ValueConflictAsync(tags, tag.Technology, value, tag.TagId, cancellationToken) is { } valueConflict)
        {
            return valueConflict;
        }

        Set("Format", tag.TagType, ContractText.Canonical(request.TagType, TagTechnology.TagTypes[tag.Technology]), v => tag.TagType = v!);
        Set(TagMappings.ValueLabelOf(tag.Technology), tag.Value, value, v => tag.Value = v);
        Set("Firmware", tag.FirmwareVersion, ContractText.TrimOrNull(request.FirmwareVersion), v => tag.FirmwareVersion = v);
        Set("Site id", tag.SiteId, ContractText.TrimOrNull(request.SiteId), v => tag.SiteId = v);
        Set("Site", tag.SiteName, ContractText.TrimOrNull(request.SiteName), v => tag.SiteName = v);
        Set("Location", tag.LocationName, ContractText.TrimOrNull(request.LocationName), v => tag.LocationName = v);

        if (changes.Count > 0)
        {
            var currentUser = currentUserAccessor.GetCurrentUser();
            tag.UpdatedBy = currentUser?.UserName;
            tag.UpdatedAt = DateTime.UtcNow;

            await tags.UpdateAsync(tag.Id, tag, cancellationToken);
            await RecordAsync(events, tag, TagEventType.Updated, $"Updated: {string.Join("; ", changes)}",
                Actor(currentUser), tag.UpdatedAt.Value, cancellationToken);
            await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);
        }

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>Assigns the tag to an asset, or reassigns it when it is linked to another asset.</summary>
    private static async Task<IResult> AssignTagAsync(
        string tagId,
        AssignTagRequest request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        if (tag.Status == TagStatus.Inactive)
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is inactive; reactivate it before assigning it." });
        }

        var asset = await assets.FindAsync(request.AssetCode!, cancellationToken);
        if (asset is null)
        {
            return UnknownAsset(request.AssetCode!);
        }

        if (tag.Status == TagStatus.Assigned
            && string.Equals(tag.AssetRegistrationId, asset.AssetRegistrationId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is already assigned to {TagMappings.AssetDisplay(asset)}." });
        }

        var previous = tag.Status == TagStatus.Assigned ? TagMappings.AssetDisplay(tag) : null;
        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;

        LinkAsset(tag, asset, Actor(currentUser), now);
        tag.SiteId = ContractText.TrimOrNull(request.SiteId) ?? tag.SiteId;
        tag.SiteName = ContractText.TrimOrNull(request.SiteName) ?? tag.SiteName;
        tag.LocationName = ContractText.TrimOrNull(request.LocationName) ?? tag.LocationName;
        tag.UpdatedBy = currentUser?.UserName;
        tag.UpdatedAt = now;

        await tags.UpdateAsync(tag.Id, tag, cancellationToken);

        var details = previous is null
            ? $"Assigned to {TagMappings.AssetDisplay(asset)}"
            : $"Reassigned from {previous} to {TagMappings.AssetDisplay(asset)}";
        await RecordAsync(events, tag, previous is null ? TagEventType.Assigned : TagEventType.Reassigned,
            WithNote(details, request.Note), Actor(currentUser), now, cancellationToken);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>Unlinks the tag from its asset and puts it back in stock as Available.</summary>
    private static async Task<IResult> UnassignTagAsync(
        string tagId,
        UnassignTagRequest? request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        request ??= new UnassignTagRequest(null);
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        if (tag.Status != TagStatus.Assigned)
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is not assigned to an asset." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        var details = $"Unassigned from {TagMappings.AssetDisplay(tag)}";
        var unlinked = AssetOf(tag);

        UnlinkAsset(tag, TagStatus.Available);
        tag.UpdatedBy = currentUser?.UserName;
        tag.UpdatedAt = now;

        await tags.UpdateAsync(tag.Id, tag, cancellationToken);
        await RecordAsync(events, tag, TagEventType.Unassigned, WithNote(details, request.Reason),
            Actor(currentUser), now, cancellationToken, unlinked);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>Takes the tag out of use (lost, damaged, retired); an assigned tag is unassigned first.</summary>
    private static async Task<IResult> DeactivateTagAsync(
        string tagId,
        DeactivateTagRequest request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        if (tag.Status == TagStatus.Inactive)
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is already inactive." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        var reason = request.Reason!.Trim();

        if (tag.Status == TagStatus.Assigned)
        {
            var unassigned = $"Unassigned from {TagMappings.AssetDisplay(tag)}";
            var unlinked = AssetOf(tag);
            UnlinkAsset(tag, TagStatus.Available);
            await RecordAsync(events, tag, TagEventType.Unassigned, WithNote(unassigned, "tag deactivated"),
                Actor(currentUser), now, cancellationToken, unlinked);
        }

        tag.Status = TagStatus.Inactive;
        tag.InactiveReason = reason;
        tag.UpdatedBy = currentUser?.UserName;
        tag.UpdatedAt = now;

        await tags.UpdateAsync(tag.Id, tag, cancellationToken);
        await RecordAsync(events, tag, TagEventType.Deactivated, $"Deactivated: {reason}",
            Actor(currentUser), now, cancellationToken);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>Puts an inactive tag back in stock as Available.</summary>
    private static async Task<IResult> ReactivateTagAsync(
        string tagId,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        if (tag.Status != TagStatus.Inactive)
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is not inactive." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;

        tag.Status = TagStatus.Available;
        tag.InactiveReason = null;
        tag.UpdatedBy = currentUser?.UserName;
        tag.UpdatedAt = now;

        await tags.UpdateAsync(tag.Id, tag, cancellationToken);
        await RecordAsync(events, tag, TagEventType.Reactivated, "Reactivated and returned to stock",
            Actor(currentUser), now, cancellationToken);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>A gateway, RFID portal or handheld scanner read the tag: updates last seen, location, battery and signal.</summary>
    private static async Task<IResult> RecordReadAsync(
        string tagId,
        RecordTagReadRequest request,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        var readAt = request.ReadAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var readBy = request.ReadBy!.Trim();

        // An older read arriving late is still logged, but does not overwrite a newer one.
        if (tag.LastSeenAt is null || readAt >= tag.LastSeenAt)
        {
            tag.LastSeenAt = readAt;
            tag.LastSeenBy = readBy;
            tag.BatteryPercent = request.BatteryPercent ?? tag.BatteryPercent;
            tag.SignalDbm = request.SignalDbm ?? tag.SignalDbm;
            tag.SiteId = ContractText.TrimOrNull(request.SiteId) ?? tag.SiteId;
            tag.SiteName = ContractText.TrimOrNull(request.SiteName) ?? tag.SiteName;
            tag.LocationName = ContractText.TrimOrNull(request.LocationName) ?? tag.LocationName;
            await tags.UpdateAsync(tag.Id, tag, cancellationToken);
        }

        await RecordAsync(events, tag, TagEventType.Read, $"Last read by {readBy}",
            TagEventType.SystemActor, readAt, cancellationToken);

        return await DetailsAsync(tag, tags, events, cancellationToken);
    }

    /// <summary>
    /// Deletes a tag; its audit trail is kept and its tag id can be registered again. An assigned tag is refused
    /// unless <paramref name="unassign"/> is true (the UI confirmed it), in which case it is unassigned first.
    /// </summary>
    private static async Task<IResult> DeleteTagAsync(
        string tagId,
        bool? unassign,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tag = await tags.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null)
        {
            return NotFound(tagId);
        }

        var actor = Actor(currentUserAccessor.GetCurrentUser());
        var now = DateTime.UtcNow;

        if (tag.Status == TagStatus.Assigned)
        {
            if (unassign != true)
            {
                return Results.Conflict(new { message = $"Unassign the tag from {TagMappings.AssetDisplay(tag)} first." });
            }

            var details = $"Unassigned from {TagMappings.AssetDisplay(tag)}";
            var unlinked = AssetOf(tag);
            UnlinkAsset(tag, TagStatus.Available);
            await RecordAsync(events, tag, TagEventType.Unassigned, WithNote(details, "tag deleted"),
                actor, now, cancellationToken, unlinked);
        }

        if (!await tags.DeleteAsync(tag.Id, cancellationToken))
        {
            return NotFound(tagId);
        }

        tag.Status = null;
        await RecordAsync(events, tag, TagEventType.Deleted, "Deleted from Tag Management", actor, now, cancellationToken);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // ------------------------------------------------------------ Helpers

    /// <summary>
    /// A tag's status follows its asset link: tags still linked to an asset that was deleted (or no longer exists)
    /// are unlinked and put back in stock as Available. Inactive tags keep their manual status.
    /// </summary>
    private static async Task ReleaseTagsOfDeletedAssetsAsync(
        IReadOnlyList<TrackingTag> candidates,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        var linked = candidates
            .Where(tag => tag.Status == TagStatus.Assigned && !string.IsNullOrWhiteSpace(tag.AssetRegistrationId))
            .ToList();
        if (linked.Count == 0)
        {
            return;
        }

        var existing = await assets.GetExistingRegistrationIdsAsync(
            linked.Select(tag => tag.AssetRegistrationId!).Distinct().ToList(), cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var tag in linked.Where(tag => !existing.Contains(tag.AssetRegistrationId!)))
        {
            var details = $"Unassigned from {TagMappings.AssetDisplay(tag)} (asset deleted)";
            var unlinked = AssetOf(tag);
            UnlinkAsset(tag, TagStatus.Available);
            tag.UpdatedBy = TagEventType.SystemActor;
            tag.UpdatedAt = now;
            await tags.UpdateAsync(tag.Id, tag, cancellationToken);
            await RecordAsync(events, tag, TagEventType.Unassigned, details, TagEventType.SystemActor, now, cancellationToken, unlinked);
        }
    }

    private static void LinkAsset(TrackingTag tag, TagAsset asset, string assignedBy, DateTime assignedAt)
    {
        tag.AssetRegistrationId = asset.AssetRegistrationId;
        tag.AssetCode = asset.AssetCode;
        tag.AssetName = asset.AssetName;
        tag.AssignedAt = assignedAt;
        tag.AssignedBy = assignedBy;
        tag.Status = TagStatus.Assigned;
    }

    private static void UnlinkAsset(TrackingTag tag, string status)
    {
        tag.AssetRegistrationId = null;
        tag.AssetCode = null;
        tag.AssetName = null;
        tag.AssignedAt = null;
        tag.AssignedBy = null;
        tag.Status = status;
    }

    private static TagAsset? AssetOf(TrackingTag tag)
    {
        return tag.AssetRegistrationId is null ? null : new TagAsset(tag.AssetRegistrationId, tag.AssetCode, tag.AssetName);
    }

    /// <summary>Writes an Audit Trail row, with the tag's asset or, for an unassignment, the asset it was unlinked from.</summary>
    private static Task RecordAsync(
        ITagEventRepository events,
        TrackingTag tag,
        string eventType,
        string details,
        string performedBy,
        DateTime occurredAt,
        CancellationToken cancellationToken,
        TagAsset? asset = null)
    {
        asset ??= AssetOf(tag);
        return events.CreateAsync(new TagEvent
        {
            TagId = tag.TagId,
            Technology = tag.Technology,
            EventType = eventType,
            Details = details,
            AssetRegistrationId = asset?.AssetRegistrationId,
            AssetCode = asset?.AssetCode,
            AssetName = asset?.AssetName,
            StatusAfter = tag.Status,
            PerformedBy = performedBy,
            OccurredAt = occurredAt,
            CreatedBy = performedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = tag.ClientId,
            TenantId = tag.TenantId,
            IsDeleted = false
        }, cancellationToken);
    }

    private static async Task<IResult> DetailsAsync(
        TrackingTag tag,
        ITrackingTagRepository tags,
        ITagEventRepository events,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(tag, tags, cancellationToken);
        var history = (await events.GetByTagIdAsync(tag.TagId, cancellationToken))
            .Where(tagEvent => TagEventType.Assignment.Contains(tagEvent.EventType))
            .ToList();
        return Results.Ok(tag.ToDetails(validation, history));
    }

    /// <summary>Validation of one tag: its value must fit the technology and not be carried by another tag.</summary>
    private static async Task<TagValidationResult> ValidateAsync(
        TrackingTag tag,
        ITrackingTagRepository tags,
        CancellationToken cancellationToken)
    {
        if (TagValueFormat.Problem(tag.Technology, tag.Value) is { } problem)
        {
            return TagValidationResult.Invalid(problem);
        }

        var duplicateOf = await tags.FindTagIdByValueAsync(tag.Technology, tag.Value!, tag.TagId, cancellationToken);
        return duplicateOf is null
            ? TagValidationResult.Valid
            : TagValidationResult.Invalid($"Same {TagMappings.ValueLabelOf(tag.Technology)} as {duplicateOf}");
    }

    /// <summary>Validation of every tag at once, keyed by the tag's database id.</summary>
    private static Dictionary<string, TagValidationResult> Validate(IReadOnlyList<TrackingTag> all)
    {
        var holders = all
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Value))
            .GroupBy(tag => (tag.Technology, Value: tag.Value!.ToUpperInvariant()))
            .ToDictionary(group => group.Key, group => group.Select(tag => tag.TagId).ToList());

        return all.ToDictionary(tag => tag.Id, tag =>
        {
            if (TagValueFormat.Problem(tag.Technology, tag.Value) is { } problem)
            {
                return TagValidationResult.Invalid(problem);
            }

            var other = holders[(tag.Technology, tag.Value!.ToUpperInvariant())].FirstOrDefault(id => id != tag.TagId);
            return other is null
                ? TagValidationResult.Valid
                : TagValidationResult.Invalid($"Same {TagMappings.ValueLabelOf(tag.Technology)} as {other}");
        });
    }

    private static async Task<IResult?> ValueConflictAsync(
        ITrackingTagRepository tags,
        string technology,
        string? value,
        string? exceptTagId,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return null;
        }

        var holder = await tags.FindTagIdByValueAsync(technology, value, exceptTagId, cancellationToken);
        return holder is null
            ? null
            : Results.Conflict(new { message = $"{TagMappings.ValueLabelOf(technology)} '{value}' is already registered on Tag ID '{holder}'." });
    }

    private static bool Matches(TrackingTag tag, string search)
    {
        return new[] { tag.TagId, tag.Value, tag.AssetRegistrationId, tag.AssetCode, tag.AssetName, tag.SiteName, tag.LocationName }
            .Any(field => field?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string Place(TrackingTag tag)
    {
        return tag.LocationName is null ? tag.SiteName! : $"{tag.SiteName} / {tag.LocationName}";
    }

    private static string WithNote(string details, string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? details : $"{details} ({note.Trim()})";
    }

    private static string Actor(CurrentUser? currentUser)
    {
        return currentUser?.UserName ?? TagEventType.SystemActor;
    }

    private static IResult UnknownAsset(string assetCode)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["AssetCode"] = [$"No asset found with code or registration id '{assetCode.Trim()}'"]
        });
    }

    private static IResult NotFound(string tagId)
    {
        return Results.NotFound(new { message = $"Tag ID '{tagId}' was not found." });
    }
}
