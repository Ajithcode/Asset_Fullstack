using System.Globalization;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace A360.Asset.Repository.Activity;

/// <summary>Kinds of activity on an asset; the Type column of the Recent Activity table.</summary>
public static class AssetActivityTypes
{
    public const string Asset = "Asset";
    public const string Component = "Component";
    public const string ServiceRequest = "Service Request";
    public const string Maintenance = "Maintenance";
    public const string Health = "Health";
    public const string Criticality = "Criticality";
    public const string RiskAction = "Risk Action";
    public const string Assignment = "Assignment";
    public const string Document = "Document";
    public const string Image = "Image";
    public const string Lifecycle = "Lifecycle";
    public const string Disposal = "Disposal";

    public static readonly IReadOnlyList<string> All =
    [
        Asset, Component, ServiceRequest, Maintenance, Health, Criticality,
        RiskAction, Assignment, Document, Image, Lifecycle, Disposal
    ];
}

/// <param name="HasTime">False when only the date is known (e.g. an assessment date), so no time should be shown.</param>
/// <param name="Reference">Number of the record the activity belongs to, e.g. "SR-2026-121" or "WO-2026-083".</param>
/// <param name="SourceId">Id of that record, for linking to it.</param>
public sealed record AssetActivityItem(
    DateTime At,
    bool HasTime,
    string Type,
    string Title,
    string? Description,
    string? By,
    string? Reference,
    string? SourceId);

public interface IAssetActivityRepository
{
    /// <summary>Everything recorded for the asset, newest first; empty when the asset does not exist.</summary>
    Task<IReadOnlyList<AssetActivityItem>> GetActivityAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds an asset's activity timeline from the records that belong to it: the event log (asset and component
/// changes, including deletions), service requests, Maintenance work orders, condition and criticality assessments,
/// risk mitigation actions, assignments, documents, images, lifecycle stages and events, and disposal requests.
/// </summary>
public sealed class AssetActivityRepository(IMongoDatabase database) : IAssetActivityRepository
{
    // Written by the Maintenance service in the shared database; asset_id holds the asset tag or registration id.
    private const string WorkOrderCollection = "work_orders";

    // Upper bound per source, so an asset with a very long history still loads quickly.
    private const int MaxPerSource = 500;

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public async Task<IReadOnlyList<AssetActivityItem>> GetActivityAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        var key = assetRegistrationId.Trim();
        var asset = await database.GetCollection<AssetRegistration>(AssetRegistrationRepository.CollectionName)
            .Find(
                Builders<AssetRegistration>.Filter.Eq(item => item.AssetRegistrationId, key),
                new FindOptions { Collation = CaseInsensitive })
            .Project<AssetRegistration>(Builders<AssetRegistration>.Projection
                .Include(item => item.AssetRegistrationId)
                .Include(item => item.BasicInformation.AssetTag)
                .Include(item => item.BasicInformation.AssetName)
                .Include(item => item.CreatedAt)
                .Include(item => item.CreatedBy)
                .Include(item => item.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

        if (asset is null)
        {
            return [];
        }

        var id = asset.AssetRegistrationId;
        var tag = string.IsNullOrWhiteSpace(asset.BasicInformation?.AssetTag) ? null : asset.BasicInformation.AssetTag.Trim();

        var sources = new[]
        {
            AssetEventsAsync(asset, cancellationToken),
            ComponentEventsAsync(id, cancellationToken),
            ServiceRequestsAsync(id, cancellationToken),
            WorkOrdersAsync(id, tag, cancellationToken),
            ConditionAssessmentsAsync(id, cancellationToken),
            CriticalityAssessmentsAsync(id, cancellationToken),
            RiskActionsAsync(id, cancellationToken),
            AssignmentsAsync(id, cancellationToken),
            DocumentsAsync(id, cancellationToken),
            ImagesAsync(id, cancellationToken),
            LifecycleAsync(id, cancellationToken),
            DisposalsAsync(id, cancellationToken)
        };

        var results = await Task.WhenAll(sources);

        return results
            .SelectMany(items => items)
            .OrderByDescending(item => item.At)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Registration, edits and deletion of the asset, from the event log; registration date as a fallback.</summary>
    private async Task<IReadOnlyList<AssetActivityItem>> AssetEventsAsync(AssetRegistration asset, CancellationToken cancellationToken)
    {
        var logs = await EventLogs()
            .Find(log => log.EntityType == "AssetRegistration" && log.EntityId == asset.AssetRegistrationId)
            .SortByDescending(log => log.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        // Ids can be reused after records are cleared, so older logs under the same id belong to another asset.
        var since = asset.CreatedAt?.AddMinutes(-1);
        logs = logs.Where(log => log.CreatedAt is not null && (since is null || log.CreatedAt >= since)).ToList();

        var items = logs
            .Select(log => new AssetActivityItem(
                log.CreatedAt!.Value,
                true,
                AssetActivityTypes.Asset,
                log.Action switch
                {
                    EventAction.Created => "Asset registered",
                    EventAction.Deleted => "Asset deleted",
                    _ => "Asset details updated"
                },
                log.Details ?? log.EntityName,
                log.PerformedBy ?? (log.Action == EventAction.Created ? asset.CreatedBy : null),
                asset.AssetRegistrationId,
                null))
            .ToList();

        // Assets registered before event logging (or imported) still show when they were registered.
        if (!logs.Any(log => log.Action == EventAction.Created) && asset.CreatedAt is not null)
        {
            items.Add(new AssetActivityItem(
                asset.CreatedAt.Value,
                true,
                AssetActivityTypes.Asset,
                "Asset registered",
                asset.BasicInformation?.AssetName,
                asset.CreatedBy,
                asset.AssetRegistrationId,
                null));
        }

        return items;
    }

    /// <summary>
    /// Component additions, edits and removals. The event log names a component "{assetRegistrationId} - {name}";
    /// components with no logged creation fall back to their own created date.
    /// </summary>
    private async Task<IReadOnlyList<AssetActivityItem>> ComponentEventsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var prefix = $"{assetRegistrationId} - ";
        var logs = await EventLogs()
            .Find(Builders<EventLog>.Filter.And(
                Builders<EventLog>.Filter.Eq(log => log.EntityType, "AssetComponent"),
                Builders<EventLog>.Filter.Regex(log => log.EntityName, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(prefix)}"))))
            .SortByDescending(log => log.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        var items = logs
            .Where(log => log.CreatedAt is not null)
            .Select(log =>
            {
                var name = log.EntityName[prefix.Length..];
                var verb = log.Action switch
                {
                    EventAction.Created => "added",
                    EventAction.Deleted => "removed",
                    _ => "updated"
                };
                return new AssetActivityItem(
                    log.CreatedAt!.Value,
                    true,
                    AssetActivityTypes.Component,
                    $"Component {verb}: {name}",
                    log.EntityId,
                    log.PerformedBy,
                    log.EntityId,
                    null);
            })
            .ToList();

        var loggedTags = logs
            .Where(log => log.Action == EventAction.Created)
            .Select(log => log.EntityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var components = await database.GetCollection<AssetComponent>(AssetComponentRepository.CollectionName)
            .Find(component => component.AssetRegistrationId == assetRegistrationId && !component.IsDeleted)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        items.AddRange(components
            .Where(component => component.CreatedAt is not null && !loggedTags.Contains(component.ComponentTag))
            .Select(component => new AssetActivityItem(
                component.CreatedAt!.Value,
                true,
                AssetActivityTypes.Component,
                $"Component added: {component.ComponentName}",
                component.ComponentTag,
                component.CreatedBy,
                component.ComponentTag,
                component.Id)));

        return items;
    }

    /// <summary>Every update on the asset's service requests: raised, status changes, comments, linked work orders.</summary>
    private async Task<IReadOnlyList<AssetActivityItem>> ServiceRequestsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var requests = await database.GetCollection<AssetServiceRequest>(AssetServiceRequestRepository.CollectionName)
            .Find(request => request.Asset.AssetRegistrationId == assetRegistrationId && !request.IsDeleted)
            .SortByDescending(request => request.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        var items = new List<AssetActivityItem>();
        foreach (var request in requests)
        {
            var number = string.IsNullOrWhiteSpace(request.RequestNumber) ? "Service request" : request.RequestNumber;

            if (request.Updates.Count == 0)
            {
                var raisedAt = request.SubmittedAt ?? request.CreatedAt;
                if (raisedAt is not null)
                {
                    items.Add(new AssetActivityItem(
                        raisedAt.Value, true, AssetActivityTypes.ServiceRequest, $"{number} raised",
                        request.Title, request.RequestedBy?.Name ?? request.CreatedBy, request.RequestNumber, request.Id));
                }

                continue;
            }

            foreach (var update in request.Updates)
            {
                var (title, description) = update.UpdateType switch
                {
                    "Created" => ($"{number} raised", request.Title),
                    "Status Change" => ($"{number} · {update.Status ?? "Status updated"}", update.Message ?? request.Title),
                    "Comment" => ($"{number} · Comment added", update.Message),
                    "Work Order" => ($"{number} · Work order linked", update.Message),
                    _ => ($"{number} · {(string.IsNullOrWhiteSpace(update.Action) ? "Updated" : update.Action)}", update.Message)
                };

                items.Add(new AssetActivityItem(
                    update.CreatedAt, true, AssetActivityTypes.ServiceRequest, title,
                    description, update.UserName, request.RequestNumber, request.Id));
            }
        }

        return items;
    }

    /// <summary>
    /// Maintenance work orders on the asset, one row per work order with its latest state
    /// ("WO-2026-083 · Preventive scheduled"), plus a row for creation when it was later changed.
    /// </summary>
    private async Task<IReadOnlyList<AssetActivityItem>> WorkOrdersAsync(string assetRegistrationId, string? assetTag, CancellationToken cancellationToken)
    {
        var keys = assetTag is null ? new[] { assetRegistrationId } : new[] { assetRegistrationId, assetTag };
        var filter = Builders<WorkOrderActivityReadModel>.Filter;

        var workOrders = await database.GetCollection<WorkOrderActivityReadModel>(WorkOrderCollection)
            .Find(filter.Ne(order => order.IsDeleted, true) & filter.In(order => order.AssetId, keys), new FindOptions { Collation = CaseInsensitive })
            .SortByDescending(order => order.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        var items = new List<AssetActivityItem>();
        foreach (var order in workOrders)
        {
            var workType = string.IsNullOrWhiteSpace(order.WorkType) ? "Work order" : order.WorkType.Trim();
            var history = order.History.OrderBy(entry => entry.At).ToList();
            var created = history.FirstOrDefault(entry => entry.Action == "Created");
            var latest = history.LastOrDefault();
            var createdAt = created?.At ?? order.CreatedAt;
            var latestAt = latest?.At ?? order.UpdatedAt ?? createdAt;

            if (latestAt is not null)
            {
                items.Add(new AssetActivityItem(
                    latestAt.Value,
                    true,
                    AssetActivityTypes.Maintenance,
                    $"{order.WorkOrderId} · {workType} {DescribeStatus(order.Status)}",
                    order.Title,
                    latest?.By ?? order.AssignedTechnician ?? order.UpdatedBy ?? order.CreatedBy,
                    order.WorkOrderId,
                    order.Id));
            }

            if (createdAt is not null && latestAt is not null && latestAt.Value - createdAt.Value > TimeSpan.FromMinutes(1))
            {
                items.Add(new AssetActivityItem(
                    createdAt.Value,
                    true,
                    AssetActivityTypes.Maintenance,
                    $"{order.WorkOrderId} · {workType} created",
                    order.Title,
                    created?.By ?? order.CreatedBy,
                    order.WorkOrderId,
                    order.Id));
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<AssetActivityItem>> ConditionAssessmentsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var assessments = await database.GetCollection<AssetConditionAssessment>(AssetConditionAssessmentRepository.CollectionName)
            .Find(assessment => assessment.AssetRegistrationId == assetRegistrationId && !assessment.IsDeleted)
            .SortByDescending(assessment => assessment.AssessmentDate)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        return assessments
            .Select(assessment => new AssetActivityItem(
                assessment.AssessmentDate,
                false,
                AssetActivityTypes.Health,
                $"Health score {FormatScore(assessment.HealthScore)} ({assessment.OverallCondition})",
                assessment.Remarks,
                assessment.AssessedBy,
                null,
                assessment.Id))
            .ToList();
    }

    private async Task<IReadOnlyList<AssetActivityItem>> CriticalityAssessmentsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var assessments = await database.GetCollection<AssetCriticalityAssessment>(AssetCriticalityAssessmentRepository.CollectionName)
            .Find(assessment => assessment.AssetRegistrationId == assetRegistrationId && !assessment.IsDeleted)
            .SortByDescending(assessment => assessment.AssessmentDate)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        return assessments
            .Select(assessment => new AssetActivityItem(
                assessment.AssessmentDate,
                false,
                AssetActivityTypes.Criticality,
                $"Criticality {assessment.CriticalityLevel} (score {FormatScore(assessment.CriticalityScore)})",
                assessment.Remarks ?? assessment.AssessmentMethod,
                assessment.AssessedBy,
                null,
                assessment.Id))
            .ToList();
    }

    /// <summary>Risk mitigation actions raised for the asset, and their completion.</summary>
    private async Task<IReadOnlyList<AssetActivityItem>> RiskActionsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var actions = await database.GetCollection<AssetRiskMitigationAction>(AssetRiskMitigationActionRepository.CollectionName)
            .Find(action => action.AssetRegistrationId == assetRegistrationId && !action.IsDeleted)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        var items = new List<AssetActivityItem>();
        foreach (var action in actions)
        {
            if (action.CreatedAt is not null)
            {
                items.Add(new AssetActivityItem(
                    action.CreatedAt.Value, true, AssetActivityTypes.RiskAction, "Mitigation action added",
                    action.Action, action.CreatedBy ?? action.Owner, null, action.Id));
            }

            if (action.CompletedAt is not null)
            {
                items.Add(new AssetActivityItem(
                    action.CompletedAt.Value, true, AssetActivityTypes.RiskAction, "Mitigation action completed",
                    action.Action, action.UpdatedBy ?? action.Owner, null, action.Id));
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<AssetActivityItem>> AssignmentsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var assignments = await database.GetCollection<AssetAssignmentRecord>(AssetAssignmentRepository.CollectionName)
            .Find(Builders<AssetAssignmentRecord>.Filter.And(
                Builders<AssetAssignmentRecord>.Filter.Eq(record => record.IsDeleted, false),
                Builders<AssetAssignmentRecord>.Filter.ElemMatch(record => record.Assets, item => item.AssetRegistrationId == assetRegistrationId)))
            .SortByDescending(record => record.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        return assignments
            .Where(record => record.CreatedAt is not null)
            .Select(record =>
            {
                var assignee = record.Employee?.EmployeeName
                    ?? record.Department?.DepartmentName
                    ?? record.OtherEntity?.EntityName
                    ?? record.Location?.LocationPath
                    ?? record.Location?.SiteName;
                var title = string.IsNullOrWhiteSpace(assignee) ? "Asset assigned" : $"Assigned to {assignee}";
                var description = string.Join(" · ", new[] { record.AssignmentType, record.Status, record.Purpose }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

                return new AssetActivityItem(
                    record.CreatedAt!.Value, true, AssetActivityTypes.Assignment, title,
                    description.Length == 0 ? null : description, record.CreatedBy, record.AssignmentNumber, record.Id);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<AssetActivityItem>> DocumentsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var documents = await database.GetCollection<AssetDocumentRecord>(AssetDocumentRepository.CollectionName)
            .Find(document => document.AssetRegistrationId == assetRegistrationId && !document.IsDeleted)
            .SortByDescending(document => document.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        return documents
            .Where(document => document.CreatedAt is not null)
            .Select(document => new AssetActivityItem(
                document.CreatedAt!.Value, true, AssetActivityTypes.Document, $"Document uploaded: {document.DocumentName}",
                JoinParts(document.DocumentType, string.IsNullOrWhiteSpace(document.Version) ? null : $"v{document.Version}"),
                document.CreatedBy, null, document.Id))
            .ToList();
    }

    private async Task<IReadOnlyList<AssetActivityItem>> ImagesAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var images = await database.GetCollection<AssetImage>(AssetImageRepository.CollectionName)
            .Find(image => image.AssetRegistrationId == assetRegistrationId && !image.IsDeleted)
            .SortByDescending(image => image.CreatedAt)
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        return images
            .Where(image => image.CreatedAt is not null)
            .Select(image => new AssetActivityItem(
                image.CreatedAt!.Value, true, AssetActivityTypes.Image, $"Image uploaded: {image.ImageName}",
                JoinParts(image.ImageType, image.Description), image.CreatedBy, null, image.Id))
            .ToList();
    }

    /// <summary>Lifecycle stage changes and manual lifecycle events.</summary>
    private async Task<IReadOnlyList<AssetActivityItem>> LifecycleAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var record = await database.GetCollection<AssetLifecycleRecord>(AssetLifecycleRepository.CollectionName)
            .Find(item => item.AssetRegistrationId == assetRegistrationId && !item.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return [];
        }

        var stages = record.Stages.Select(stage => new AssetActivityItem(
            stage.UpdatedAt, true, AssetActivityTypes.Lifecycle, $"Stage {Humanize(stage.Key)} · {stage.Status}",
            stage.Notes, stage.UpdatedBy ?? stage.Owner, null, record.Id));

        var events = record.Events.Select(item => new AssetActivityItem(
            item.Date, false, AssetActivityTypes.Lifecycle, item.Title,
            string.IsNullOrWhiteSpace(item.Kind) ? null : item.Kind, item.By, item.EventId, record.Id));

        return stages.Concat(events).ToList();
    }

    private async Task<IReadOnlyList<AssetActivityItem>> DisposalsAsync(string assetRegistrationId, CancellationToken cancellationToken)
    {
        var requests = await database.GetCollection<AssetDisposalRequest>(AssetDisposalRequestRepository.CollectionName)
            .Find(Builders<AssetDisposalRequest>.Filter.And(
                Builders<AssetDisposalRequest>.Filter.Eq(request => request.IsDeleted, false),
                Builders<AssetDisposalRequest>.Filter.ElemMatch(request => request.Assets, item => item.AssetRegistrationId == assetRegistrationId)))
            .Limit(MaxPerSource)
            .ToListAsync(cancellationToken);

        var items = new List<AssetActivityItem>();
        foreach (var request in requests)
        {
            var number = string.IsNullOrWhiteSpace(request.RequestNumber) ? "Disposal request" : request.RequestNumber;
            var raisedAt = request.SubmittedAt ?? request.CreatedAt;
            if (raisedAt is not null)
            {
                items.Add(new AssetActivityItem(
                    raisedAt.Value, true, AssetActivityTypes.Disposal, $"{number} raised",
                    JoinParts(request.RequestTitle, request.Reason), request.SubmittedBy ?? request.RequestedBy?.Name ?? request.CreatedBy,
                    request.RequestNumber, request.Id));
            }

            if (request.UpdatedAt is not null && raisedAt is not null
                && request.UpdatedAt.Value - raisedAt.Value > TimeSpan.FromMinutes(1)
                && !string.IsNullOrWhiteSpace(request.Status))
            {
                items.Add(new AssetActivityItem(
                    request.UpdatedAt.Value, true, AssetActivityTypes.Disposal, $"{number} · {request.Status}",
                    request.CancelledReason ?? request.RequestTitle, request.UpdatedBy, request.RequestNumber, request.Id));
            }
        }

        return items;
    }

    private IMongoCollection<EventLog> EventLogs() => database.GetCollection<EventLog>(EventLogRepository.CollectionName);

    /// <summary>"Scheduled" → "scheduled", "In Progress" → "in progress", so it reads "Preventive scheduled".</summary>
    private static string DescribeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "updated" : status.Trim().ToLowerInvariant();

    private static string FormatScore(decimal score) => Math.Round(score, 1).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>"in_service" or "inService" → "In Service".</summary>
    private static string Humanize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var spaced = System.Text.RegularExpressions.Regex.Replace(key.Replace('_', ' ').Replace('-', ' '), "(?<=[a-z])(?=[A-Z])", " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }

    private static string? JoinParts(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return text.Length == 0 ? null : text;
    }
}

/// <summary>The fields of a Maintenance work order the activity timeline needs.</summary>
[BsonIgnoreExtraElements]
public sealed class WorkOrderActivityReadModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("work_order_id")] public string WorkOrderId { get; set; } = string.Empty;
    [BsonElement("asset_id")] public string? AssetId { get; set; }
    [BsonElement("work_type")] public string? WorkType { get; set; }
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("assigned_technician")] public string? AssignedTechnician { get; set; }
    [BsonElement("created_by")] public string? CreatedBy { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("updated_by")] public string? UpdatedBy { get; set; }
    [BsonElement("updated_at")] public DateTime? UpdatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
    [BsonElement("history")] public List<WorkOrderActivityHistoryEntry> History { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderActivityHistoryEntry
{
    [BsonElement("at")] public DateTime At { get; set; }
    [BsonElement("by")] public string? By { get; set; }
    [BsonElement("action")] public string Action { get; set; } = string.Empty;
    [BsonElement("details")] public string? Details { get; set; }
}
