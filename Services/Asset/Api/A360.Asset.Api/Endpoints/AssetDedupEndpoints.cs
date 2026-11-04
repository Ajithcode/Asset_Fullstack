using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Asset Merge / Dedup screen: duplicate-detection rules, the duplicate groups found among registered assets,
/// "not a duplicate" dismissals, and merging duplicates into one primary asset.
/// </summary>
/// <remarks>
/// Groups are worked out on each request from the current assets, rules and dismissals, so they always reflect
/// the register. A merge updates the primary asset with the chosen values and soft-deletes the merged assets.
/// </remarks>
public static class AssetDedupEndpoints
{
    private const string MergeEntityType = "AssetMerge";
    private const string DismissalEntityType = "AssetDedupDismissal";
    private const string AssetEntityType = "AssetRegistration";
    private const int MaxNotesLength = 500;
    private const int MaxGroupPageSize = 1000;

    public static IEndpointRouteBuilder MapAssetDedupEndpoints(this IEndpointRouteBuilder routes)
    {
        var dedup = routes.MapGroup("/api/asset-dedup").WithTags("Asset Merge / Dedup");

        dedup.MapGet("/rules", GetRulesAsync).WithName("GetAssetDedupRules");
        dedup.MapPut("/rules", SaveRulesAsync).WithName("SaveAssetDedupRules");
        dedup.MapPost("/scan", ScanAsync).WithName("ScanAssetDuplicates");
        dedup.MapGet("/groups", GetGroupsAsync).WithName("GetAssetDuplicateGroups");
        dedup.MapGet("/groups/{groupId}", GetGroupAsync).WithName("GetAssetDuplicateGroup");
        dedup.MapGet("/dismissals", GetDismissalsAsync).WithName("GetAssetDedupDismissals");
        dedup.MapPost("/dismissals", DismissAsync).WithName("DismissAssetDuplicates");
        dedup.MapDelete("/dismissals/{id}", UndoDismissalAsync).WithName("UndoAssetDedupDismissal");

        var merges = routes.MapGroup("/api/asset-merges").WithTags("Asset Merge / Dedup");

        merges.MapGet("", GetMergesAsync).WithName("GetAssetMerges");
        merges.MapGet("/{id}", GetMergeByIdAsync).WithName("GetAssetMergeById");
        merges.MapPost("", MergeAsync).WithName("MergeAssets");

        return routes;
    }

    // ===== Rules =====

    private static async Task<IResult> GetRulesAsync(IAssetDedupRulesRepository rulesRepository, CancellationToken cancellationToken)
    {
        var rules = await rulesRepository.GetCurrentAsync(cancellationToken) ?? AssetDedupMatcher.DefaultRules();
        return Results.Ok(AssetDedupRulesResponse.FromEntity(rules));
    }

    private static async Task<IResult> SaveRulesAsync(
        AssetDedupRulesRequest request,
        IAssetDedupRulesRepository rulesRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var fields = (request.Fields ?? [])
            .Select(field => field?.Trim() ?? string.Empty)
            .Where(field => field.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var errors = new Dictionary<string, string[]>();
        if (fields.Count == 0)
        {
            errors["Fields"] = ["Choose at least one matching field"];
        }
        else if (fields.FirstOrDefault(field => !AssetDedupFields.Match.Contains(field)) is { } unknown)
        {
            errors["Fields"] = [$"Unknown field '{unknown}'. Allowed: {string.Join(", ", AssetDedupFields.Match)}"];
        }

        if (!(request.High <= 100 && request.High > request.Medium && request.Medium > request.Low && request.Low >= 1))
        {
            errors["Thresholds"] = ["Thresholds must go High > Medium > Low, between 1 and 100"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        var rules = await rulesRepository.GetCurrentAsync(cancellationToken);
        var isNew = rules is null;
        rules ??= new AssetDedupRules { CreatedBy = userName, CreatedAt = DateTime.UtcNow };

        rules.Fields = fields;
        rules.High = request.High;
        rules.Medium = request.Medium;
        rules.Low = request.Low;
        rules.IncludeInactive = request.IncludeInactive;
        rules.UpdatedBy = userName;
        rules.UpdatedAt = DateTime.UtcNow;

        if (isNew)
        {
            rules = await rulesRepository.CreateAsync(rules, cancellationToken);
        }
        else
        {
            await rulesRepository.UpdateAsync(rules.Id, rules, cancellationToken);
        }

        await eventLogger.LogAsync("AssetDedupRules", rules.Id, "Duplicate detection rules", isNew ? EventAction.Created : EventAction.Updated, cancellationToken);
        return Results.Ok(AssetDedupRulesResponse.FromEntity(rules));
    }

    // ===== Scan and groups =====

    private static async Task<IResult> ScanAsync(
        IAssetRegistrationRepository assetRepository,
        IAssetDedupRulesRepository rulesRepository,
        IAssetDedupDismissalRepository dismissalRepository,
        CancellationToken cancellationToken)
    {
        var (groups, rules) = await FindGroupsAsync(assetRepository, rulesRepository, dismissalRepository, cancellationToken);

        // Remember when the register was last scanned (saves the default rules on the first scan)
        var scannedAt = DateTime.UtcNow;
        rules.LastScanAt = scannedAt;
        if (string.IsNullOrEmpty(rules.Id))
        {
            rules.CreatedAt = scannedAt;
            await rulesRepository.CreateAsync(rules, cancellationToken);
        }
        else
        {
            await rulesRepository.UpdateAsync(rules.Id, rules, cancellationToken);
        }

        var counts = CountByConfidence(groups, rules);
        return Results.Ok(new AssetDedupScanResponse(
            scannedAt, groups.Count, groups.Sum(group => group.Members.Count), counts.High, counts.Medium, counts.Low));
    }

    /// <summary>
    /// Duplicate groups. Filters: confidence (of the group's best match: High, Medium or Low), category, site
    /// (first part of the location), status, search (tag, name or serial of any record in the group).
    /// </summary>
    private static async Task<IResult> GetGroupsAsync(
        IAssetRegistrationRepository assetRepository,
        IAssetDedupRulesRepository rulesRepository,
        IAssetDedupDismissalRepository dismissalRepository,
        CancellationToken cancellationToken,
        string? confidence = null,
        string? category = null,
        string? site = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxGroupPageSize ? 10 : pageSize;

        var (groups, rules) = await FindGroupsAsync(assetRepository, rulesRepository, dismissalRepository, cancellationToken);
        var counts = CountByConfidence(groups, rules);
        var query = search?.Trim();

        var filtered = groups.Where(group =>
            (string.IsNullOrWhiteSpace(confidence) || string.Equals(AssetDedupMatcher.Confidence(group.MaxScore, rules), confidence.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(category) || string.Equals(group.Primary.Record.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(site) || string.Equals(SiteOf(group.Primary.Record.Location), site.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(status) || string.Equals(group.Primary.Record.Status, status.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(query) || Records(group).Any(record =>
                record.Tag.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.SerialNumber.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(group => ToResponse(group, rules))
            .ToList();

        return Results.Ok(new AssetDedupGroupListResponse(
            items, filtered.Count, page, pageSize, counts, groups.Sum(group => group.Members.Count), rules.LastScanAt));
    }

    private static async Task<IResult> GetGroupAsync(
        string groupId,
        IAssetRegistrationRepository assetRepository,
        IAssetDedupRulesRepository rulesRepository,
        IAssetDedupDismissalRepository dismissalRepository,
        CancellationToken cancellationToken)
    {
        var (groups, rules) = await FindGroupsAsync(assetRepository, rulesRepository, dismissalRepository, cancellationToken);
        var group = groups.FirstOrDefault(candidate => string.Equals(candidate.Primary.Record.Tag, groupId, StringComparison.OrdinalIgnoreCase));

        return group is null
            ? Results.NotFound(new { message = $"No duplicate group with primary asset '{groupId}' was found." })
            : Results.Ok(ToResponse(group, rules));
    }

    // ===== Dismissals =====

    private static async Task<IResult> GetDismissalsAsync(IAssetDedupDismissalRepository dismissalRepository, CancellationToken cancellationToken)
    {
        var dismissals = await dismissalRepository.GetActiveAsync(cancellationToken);
        return Results.Ok(dismissals.Select(AssetDedupDismissalResponse.FromEntity).ToList());
    }

    /// <summary>Marks Tags[0] as not duplicating each of the other tags. Pairs already dismissed are returned as they are.</summary>
    private static async Task<IResult> DismissAsync(
        DismissAssetDuplicatesRequest request,
        IAssetDedupDismissalRepository dismissalRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tags = (request.Tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (tags.Count < 2)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Tags"] = ["Send the primary asset's tag followed by at least one duplicate's tag"]
            });
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        var primary = tags[0];
        var saved = new List<AssetDedupDismissal>();
        foreach (var other in tags.Skip(1))
        {
            var pairKey = AssetDedupMatcher.PairKey(primary, other);
            var existing = await dismissalRepository.GetByPairKeyAsync(pairKey, cancellationToken);
            if (existing is not null)
            {
                saved.Add(existing);
                continue;
            }

            var dismissal = await dismissalRepository.CreateAsync(new AssetDedupDismissal
            {
                Tags = pairKey.Split('|').ToList(),
                PairKey = pairKey,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await eventLogger.LogAsync(DismissalEntityType, dismissal.Id, $"{dismissal.Tags[0]} / {dismissal.Tags[1]} not a duplicate", EventAction.Created, cancellationToken);
            saved.Add(dismissal);
        }

        return Results.Ok(saved.Select(AssetDedupDismissalResponse.FromEntity).ToList());
    }

    private static async Task<IResult> UndoDismissalAsync(
        string id,
        IAssetDedupDismissalRepository dismissalRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var dismissal = await dismissalRepository.GetByIdAsync(id, cancellationToken);
        if (dismissal is null || dismissal.IsDeleted)
        {
            return Results.NotFound(new { message = $"Dismissal '{id}' was not found." });
        }

        dismissal.IsDeleted = true;
        dismissal.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        dismissal.UpdatedAt = DateTime.UtcNow;
        await dismissalRepository.UpdateAsync(dismissal.Id, dismissal, cancellationToken);

        await eventLogger.LogAsync(DismissalEntityType, dismissal.Id, $"{string.Join(" / ", dismissal.Tags)} not a duplicate", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // ===== Merges =====

    private static async Task<IResult> GetMergesAsync(
        IAssetMergeRepository mergeRepository,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = AssetMergeRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetMergeRepository.MaxPageSize ? AssetMergeRepository.DefaultPageSize : pageSize;

        var result = await mergeRepository.QueryAsync(search, page, pageSize, cancellationToken);
        return Results.Ok(new AssetMergeListResponse(result.Items.Select(AssetMergeResponse.FromEntity).ToList(), result.TotalCount, page, pageSize));
    }

    private static async Task<IResult> GetMergeByIdAsync(string id, IAssetMergeRepository mergeRepository, CancellationToken cancellationToken)
    {
        var merge = await mergeRepository.GetByIdAsync(id, cancellationToken);
        return merge is null || merge.IsDeleted
            ? Results.NotFound(new { message = $"Asset merge '{id}' was not found." })
            : Results.Ok(AssetMergeResponse.FromEntity(merge));
    }

    /// <summary>
    /// Merges the duplicates into the primary asset: each field in FieldSources takes the value of the named record,
    /// the merged assets are soft-deleted, and the merge is logged.
    /// </summary>
    private static async Task<IResult> MergeAsync(
        MergeAssetsRequest request,
        IAssetRegistrationRepository assetRepository,
        IAssetMergeRepository mergeRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var primaryTag = request.PrimaryTag?.Trim() ?? string.Empty;
        var mergedTags = (request.MergedTags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0 && !string.Equals(tag, primaryTag, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var fieldSources = (request.FieldSources ?? [])
            .Where(source => !string.IsNullOrWhiteSpace(source.Key) && !string.IsNullOrWhiteSpace(source.Value))
            .ToDictionary(source => source.Key.Trim(), source => source.Value.Trim(), StringComparer.Ordinal);
        var participants = new HashSet<string>(mergedTags.Append(primaryTag), StringComparer.Ordinal);

        var errors = new Dictionary<string, string[]>();
        if (primaryTag.Length == 0)
        {
            errors["PrimaryTag"] = ["Primary asset tag is required"];
        }

        if (mergedTags.Count == 0)
        {
            errors["MergedTags"] = ["Keep at least one duplicate in the merge"];
        }

        if (fieldSources.Keys.FirstOrDefault(field => !AssetDedupFields.Merge.Contains(field)) is { } unknownField)
        {
            errors["FieldSources"] = [$"Unknown field '{unknownField}'. Allowed: {string.Join(", ", AssetDedupFields.Merge)}"];
        }
        else if (fieldSources.FirstOrDefault(source => !participants.Contains(source.Value)) is { Key: not null } outsider)
        {
            errors["FieldSources"] = [$"'{outsider.Key}' must come from the primary or a merged asset, not '{outsider.Value}'"];
        }

        if (request.Notes?.Length > MaxNotesLength)
        {
            errors["Notes"] = [$"Notes can be at most {MaxNotesLength} characters"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var assets = new Dictionary<string, AssetRegistration>(StringComparer.Ordinal);
        foreach (var tag in participants)
        {
            var asset = await assetRepository.GetByAssetTagAsync(tag, cancellationToken)
                ?? await assetRepository.GetByAssetRegistrationIdAsync(tag, cancellationToken);
            if (asset is null)
            {
                return Results.NotFound(new { message = $"Asset '{tag}' was not found." });
            }

            assets[tag] = asset;
        }

        var primary = assets[primaryTag];
        foreach (var (field, sourceTag) in fieldSources)
        {
            if (!string.Equals(sourceTag, primaryTag, StringComparison.Ordinal))
            {
                CopyField(field, assets[sourceTag], primary);
            }
        }

        var userName = currentUserAccessor.GetCurrentUser()?.UserName;
        var now = DateTime.UtcNow;
        primary.UpdatedBy = userName;
        primary.UpdatedAt = now;
        await assetRepository.UpdateAsync(primary.Id, primary, cancellationToken);
        await eventLogger.LogAsync(AssetEntityType, primary.Id, Describe(primary), EventAction.Updated, cancellationToken);

        foreach (var tag in mergedTags)
        {
            var merged = assets[tag];
            merged.IsDeleted = true;
            merged.Status = "Merged";
            merged.UpdatedBy = userName;
            merged.UpdatedAt = now;
            await assetRepository.UpdateAsync(merged.Id, merged, cancellationToken);
            await eventLogger.LogAsync(AssetEntityType, merged.Id, Describe(merged), EventAction.Deleted, cancellationToken);
        }

        var finalRecord = AssetDedupMatcher.ToCandidate(primary).Record;
        var merge = await mergeRepository.CreateAsync(new AssetMerge
        {
            PrimaryTag = primaryTag,
            PrimaryAssetRegistrationId = primary.AssetRegistrationId,
            MergedTags = mergedTags,
            MergedAssetRegistrationIds = mergedTags.Select(tag => assets[tag].AssetRegistrationId).ToList(),
            FieldSources = fieldSources,
            FinalValues = AssetDedupFields.Merge.ToDictionary(field => field, field => FieldValue(finalRecord, field)),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = userName,
            CreatedAt = now
        }, cancellationToken);

        await eventLogger.LogAsync(MergeEntityType, merge.Id, $"{string.Join(", ", mergedTags)} merged into {primaryTag}", EventAction.Created, cancellationToken);
        return Results.Created($"/api/asset-merges/{merge.Id}", AssetMergeResponse.FromEntity(merge));
    }

    // ===== Helpers =====

    private static async Task<(List<AssetDedupMatcher.Group> Groups, AssetDedupRules Rules)> FindGroupsAsync(
        IAssetRegistrationRepository assetRepository,
        IAssetDedupRulesRepository rulesRepository,
        IAssetDedupDismissalRepository dismissalRepository,
        CancellationToken cancellationToken)
    {
        var rules = await rulesRepository.GetCurrentAsync(cancellationToken) ?? AssetDedupMatcher.DefaultRules();
        var assets = await assetRepository.GetAllAsync(cancellationToken);
        var dismissals = await dismissalRepository.GetActiveAsync(cancellationToken);
        var dismissed = dismissals.Select(dismissal => dismissal.PairKey).ToHashSet(StringComparer.Ordinal);

        return (AssetDedupMatcher.FindGroups(assets, rules, dismissed), rules);
    }

    private static AssetDedupCounts CountByConfidence(List<AssetDedupMatcher.Group> groups, AssetDedupRules rules)
    {
        var levels = groups.Select(group => AssetDedupMatcher.Confidence(group.MaxScore, rules)).ToList();
        return new AssetDedupCounts(levels.Count(level => level == "High"), levels.Count(level => level == "Medium"), levels.Count(level => level == "Low"));
    }

    private static AssetDedupGroupResponse ToResponse(AssetDedupMatcher.Group group, AssetDedupRules rules)
    {
        return new AssetDedupGroupResponse(
            group.Primary.Record.Tag,
            group.MaxScore,
            AssetDedupMatcher.Confidence(group.MaxScore, rules),
            group.Primary.Record,
            group.Members.Select(member => new AssetDedupMemberResponse(member.Score, AssetDedupMatcher.Confidence(member.Score, rules), member.Candidate.Record)).ToList());
    }

    private static IEnumerable<AssetDedupRecordResponse> Records(AssetDedupMatcher.Group group)
    {
        return group.Members.Select(member => member.Candidate.Record).Prepend(group.Primary.Record);
    }

    private static string SiteOf(string location) => location.Split(" - ")[0].Trim();

    /// <summary>Copies one merge field from the source asset onto the primary asset.</summary>
    private static void CopyField(string field, AssetRegistration source, AssetRegistration target)
    {
        switch (field)
        {
            case "name":
                target.BasicInformation.AssetName = source.BasicInformation.AssetName;
                break;
            case "type":
                target.Classification.AssetType = source.Classification.AssetType;
                break;
            case "model":
                target.BasicInformation.Model = source.BasicInformation.Model;
                break;
            case "serialNumber":
                target.BasicInformation.SerialNumber = source.BasicInformation.SerialNumber;
                break;
            case "category":
                target.Classification.Category = source.Classification.Category;
                target.Classification.SubCategory = source.Classification.SubCategory;
                break;
            case "location":
                // The whole location (site, building, floor, zone ids and names), so ids and path stay consistent
                target.LocationAssignment.Location = source.LocationAssignment.Location;
                break;
            case "status":
                target.BasicInformation.Status = source.BasicInformation.Status;
                break;
            case "purchaseDate":
                target.PurchaseWarranty.Purchase.PurchaseDate = source.PurchaseWarranty.Purchase.PurchaseDate;
                break;
            case "supplier":
                target.PurchaseWarranty.Purchase.SupplierVendor = source.PurchaseWarranty.Purchase.SupplierVendor;
                break;
            case "value":
                target.PurchaseWarranty.Purchase.PurchaseCost = source.PurchaseWarranty.Purchase.PurchaseCost;
                break;
        }
    }

    private static string FieldValue(AssetDedupRecordResponse record, string field) => field switch
    {
        "name" => record.Name,
        "type" => record.Type,
        "model" => record.Model,
        "serialNumber" => record.SerialNumber,
        "category" => record.Category,
        "location" => record.Location,
        "status" => record.Status,
        "purchaseDate" => record.PurchaseDate,
        "supplier" => record.Supplier,
        "value" => record.Value == 0 ? string.Empty : record.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => string.Empty
    };

    private static string Describe(AssetRegistration asset)
    {
        return asset.BasicInformation.AssetName ?? asset.BasicInformation.AssetTag ?? asset.AssetRegistrationId;
    }
}
