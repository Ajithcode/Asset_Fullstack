using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Field groups (the Field Groups tab); a group's fields are assigned and ordered through its FieldIds.</summary>
public static class AssetFieldGroupEndpoints
{
    private const string EntityType = "AssetFieldGroup";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 200;

    // Fields shown on asset forms can be grouped; component fields are not.
    private static readonly IReadOnlyCollection<string> GroupableScopes = [CustomFieldScopes.Asset, CustomFieldScopes.Category];

    public static RouteGroupBuilder MapAssetFieldGroupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-field-groups").WithTags("Asset Field Groups");

        group.MapGet("", GetGroupsAsync).WithName("GetAssetFieldGroups");
        group.MapGet("/export", ExportGroupsAsync).WithName("ExportAssetFieldGroups");
        group.MapPost("/import", ImportGroupsAsync).WithName("ImportAssetFieldGroups");
        group.MapGet("/{id}", GetGroupByIdAsync).WithName("GetAssetFieldGroupById");
        group.MapPost("", CreateGroupAsync).WithName("CreateAssetFieldGroup");
        group.MapPut("/{id}", UpdateGroupAsync).WithName("UpdateAssetFieldGroup");
        group.MapDelete("/{id}", DeleteGroupAsync).WithName("DeleteAssetFieldGroup");

        return group;
    }

    private static async Task<IResult> GetGroupsAsync(
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var groups = (await repository.GetAllAsync(cancellationToken)).Where(group => !group.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            groups = groups.Where(group =>
                group.GroupName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (group.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            groups = groups.Where(group => string.Equals(group.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var filtered = groups.OrderBy(group => group.CreatedAt).ToList();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var fieldsByGroup = await LoadFieldsByGroupAsync(fieldRepository, cancellationToken);
        return Results.Ok(new AssetFieldGroupListResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(group => AssetFieldGroupResponse.FromEntity(group, fieldsByGroup[group.Id]))
                .ToList(),
            filtered.Count,
            page,
            pageSize));
    }

    private static async Task<IResult> GetGroupByIdAsync(
        string id,
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        CancellationToken cancellationToken)
    {
        var group = await repository.GetByIdAsync(id, cancellationToken);
        return group is null || group.IsDeleted
            ? NotFound(id)
            : Results.Ok(AssetFieldGroupResponse.FromEntity(group, (await LoadFieldsByGroupAsync(fieldRepository, cancellationToken))[id]));
    }

    private static async Task<IResult> CreateGroupAsync(
        SaveAssetFieldGroupRequest request,
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var groupName = request.GroupName!.Trim();
        if (await repository.GetByNameAsync(groupName, cancellationToken) is not null)
        {
            return Results.Conflict(new { message = $"A field group named '{groupName}' already exists." });
        }

        var fieldProblem = await CheckFieldIdsAsync(request.FieldIds, fieldRepository, cancellationToken);
        if (fieldProblem is not null)
        {
            return fieldProblem;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var group = await repository.CreateAsync(
            new AssetFieldGroup
            {
                GroupName = groupName,
                Description = ContractText.TrimOrNull(request.Description),
                Status = ActiveInactiveStatuses.Normalize(request.Status),
                CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
                CreatedAt = DateTime.UtcNow,
                ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
                TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
                IsDeleted = false
            },
            cancellationToken);

        if (request.FieldIds is { } fieldIds)
        {
            await fieldRepository.SetGroupFieldsAsync(group.Id, fieldIds.Select(fieldId => fieldId.Trim()).ToList(), cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, group.Id, group.GroupName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-field-groups/{group.Id}",
            AssetFieldGroupResponse.FromEntity(group, (await LoadFieldsByGroupAsync(fieldRepository, cancellationToken))[group.Id]));
    }

    private static async Task<IResult> UpdateGroupAsync(
        string id,
        SaveAssetFieldGroupRequest request,
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var group = await repository.GetByIdAsync(id, cancellationToken);
        if (group is null || group.IsDeleted)
        {
            return NotFound(id);
        }

        var groupName = request.GroupName!.Trim();
        if (await repository.GetByNameAsync(groupName, cancellationToken) is { } sameName && sameName.Id != id)
        {
            return Results.Conflict(new { message = $"A field group named '{groupName}' already exists." });
        }

        var fieldProblem = await CheckFieldIdsAsync(request.FieldIds, fieldRepository, cancellationToken);
        if (fieldProblem is not null)
        {
            return fieldProblem;
        }

        group.GroupName = groupName;
        group.Description = ContractText.TrimOrNull(request.Description);
        group.Status = ActiveInactiveStatuses.Normalize(request.Status);
        group.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        group.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(id, group, cancellationToken))
        {
            return NotFound(id);
        }

        if (request.FieldIds is { } fieldIds)
        {
            await fieldRepository.SetGroupFieldsAsync(id, fieldIds.Select(fieldId => fieldId.Trim()).ToList(), cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, group.Id, group.GroupName, EventAction.Updated, cancellationToken);

        return Results.Ok(AssetFieldGroupResponse.FromEntity(group, (await LoadFieldsByGroupAsync(fieldRepository, cancellationToken))[id]));
    }

    private static async Task<IResult> DeleteGroupAsync(
        string id,
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        IAssetFieldLayoutRepository layoutRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var group = await repository.GetByIdAsync(id, cancellationToken);
        if (group is null || group.IsDeleted)
        {
            return NotFound(id);
        }

        if (await layoutRepository.GetUsingGroupAsync(id, cancellationToken) is { Count: > 0 } layouts)
        {
            return Results.Conflict(new
            {
                message = $"Field group '{group.GroupName}' is used in layouts: {string.Join(", ", layouts.Select(layout => layout.LayoutName))}. Remove it from them first."
            });
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        // The group's fields stay; they are just no longer in a group.
        await fieldRepository.ClearGroupAsync(id, cancellationToken);
        await eventLogger.LogAsync(EntityType, group.Id, group.GroupName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ExportGroupsAsync(
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        CancellationToken cancellationToken)
    {
        var fieldsByGroup = await LoadFieldsByGroupAsync(fieldRepository, cancellationToken);

        return Results.Ok((await repository.GetAllAsync(cancellationToken))
            .Where(group => !group.IsDeleted)
            .OrderBy(group => group.CreatedAt)
            .Select(group => new AssetFieldGroupExportItem(
                group.GroupName,
                group.Description,
                group.Status,
                fieldsByGroup[group.Id].OrderBy(field => field.DisplayOrder).Select(field => field.FieldName).ToList()))
            .ToList());
    }

    /// <summary>
    /// Imports a file produced by export. Groups are matched by name: new names are created, existing ones updated.
    /// Fields are matched by name among asset and category fields; names not found are reported and skipped.
    /// </summary>
    private static async Task<IResult> ImportGroupsAsync(
        List<AssetFieldGroupExportItem> items,
        IAssetFieldGroupRepository repository,
        IAssetCustomFieldRepository fieldRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        var fieldIdsByName = (await fieldRepository.GetByScopesAsync(GroupableScopes, cancellationToken))
            .ToDictionary(field => field.FieldName, field => field.Id);

        var created = 0;
        var updated = 0;
        var errors = new List<ImportItemError>();

        foreach (var item in items)
        {
            var validationErrors = new SaveAssetFieldGroupRequest(item.GroupName, item.Description, item.Status, null, null, null, null).Validate();
            if (validationErrors.Count > 0)
            {
                errors.Add(new ImportItemError(item.GroupName, string.Join("; ", validationErrors.SelectMany(error => error.Value))));
                continue;
            }

            var groupName = item.GroupName!.Trim();
            var group = await repository.GetByNameAsync(groupName, cancellationToken);

            if (group is null)
            {
                group = await repository.CreateAsync(
                    new AssetFieldGroup
                    {
                        GroupName = groupName,
                        Description = ContractText.TrimOrNull(item.Description),
                        Status = ActiveInactiveStatuses.Normalize(item.Status),
                        CreatedBy = currentUser?.UserName,
                        CreatedAt = DateTime.UtcNow,
                        ClientId = currentUser?.ClientId,
                        TenantId = currentUser?.TenantId
                    },
                    cancellationToken);
                await eventLogger.LogAsync(EntityType, group.Id, group.GroupName, EventAction.Created, cancellationToken);
                created++;
            }
            else
            {
                group.Description = ContractText.TrimOrNull(item.Description);
                group.Status = ActiveInactiveStatuses.Normalize(item.Status);
                group.UpdatedBy = currentUser?.UserName;
                group.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(group.Id, group, cancellationToken);
                await eventLogger.LogAsync(EntityType, group.Id, group.GroupName, EventAction.Updated, cancellationToken);
                updated++;
            }

            var fieldNames = (item.FieldNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct().ToList();
            var missing = fieldNames.Where(name => !fieldIdsByName.ContainsKey(name)).ToList();
            if (missing.Count > 0)
            {
                errors.Add(new ImportItemError(groupName, $"Fields not found and skipped: {string.Join(", ", missing)}"));
            }

            await fieldRepository.SetGroupFieldsAsync(
                group.Id,
                fieldNames.Where(fieldIdsByName.ContainsKey).Select(name => fieldIdsByName[name]).ToList(),
                cancellationToken);
        }

        return Results.Ok(new ImportResult(created, updated, errors));
    }

    private static async Task<IResult?> CheckFieldIdsAsync(
        IReadOnlyCollection<string>? fieldIds,
        IAssetCustomFieldRepository fieldRepository,
        CancellationToken cancellationToken)
    {
        if (fieldIds is null || fieldIds.Count == 0)
        {
            return null;
        }

        var groupable = (await fieldRepository.GetByScopesAsync(GroupableScopes, cancellationToken)).Select(field => field.Id).ToHashSet();
        var unknown = fieldIds.Select(fieldId => fieldId.Trim()).Where(fieldId => !groupable.Contains(fieldId)).ToList();

        return unknown.Count == 0
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["FieldIds"] = [$"These are not asset or category custom fields: {string.Join(", ", unknown)}"]
            });
    }

    private static async Task<ILookup<string, AssetCustomFieldDefinition>> LoadFieldsByGroupAsync(
        IAssetCustomFieldRepository fieldRepository,
        CancellationToken cancellationToken)
    {
        return (await fieldRepository.GetByScopesAsync(GroupableScopes, cancellationToken))
            .Where(field => field.GroupId is not null)
            .ToLookup(field => field.GroupId!);
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Field group '{id}' was not found." });
    }
}
