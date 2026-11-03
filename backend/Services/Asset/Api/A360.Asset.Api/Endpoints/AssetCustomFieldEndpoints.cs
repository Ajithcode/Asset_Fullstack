using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Custom field definitions for the Asset Fields, Component Fields and Category Specific Fields tabs (told apart by scope).</summary>
public static class AssetCustomFieldEndpoints
{
    private const string EntityType = "AssetCustomFieldDefinition";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAssetCustomFieldEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-custom-fields").WithTags("Asset Custom Fields");

        group.MapGet("", GetFieldsAsync).WithName("GetAssetCustomFields");
        group.MapGet("/export", ExportFieldsAsync).WithName("ExportAssetCustomFields");
        group.MapPost("/import", ImportFieldsAsync).WithName("ImportAssetCustomFields");
        group.MapGet("/{id}", GetFieldByIdAsync).WithName("GetAssetCustomFieldById");
        group.MapPost("", CreateFieldAsync).WithName("CreateAssetCustomField");
        group.MapPut("/{id}", UpdateFieldAsync).WithName("UpdateAssetCustomField");
        group.MapDelete("/{id}", DeleteFieldAsync).WithName("DeleteAssetCustomField");

        return group;
    }

    private static async Task<IResult> GetFieldsAsync(
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken,
        string? scope = null,
        string? search = null,
        string? fieldType = null,
        string? status = null,
        string? groupId = null,
        string? appliesToCategoryId = null,
        string? componentType = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var scopes = CustomFieldScopes.Normalize(scope) is { } listedScope ? [listedScope] : CustomFieldScopes.All.ToList();
        var fields = (await repository.GetByScopesAsync(scopes, cancellationToken)).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            fields = fields.Where(field =>
                field.FieldName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || field.DisplayLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        fields = WhereEquals(fields, field => field.FieldType, fieldType);
        fields = WhereEquals(fields, field => field.Status, status);
        fields = WhereEquals(fields, field => field.GroupId, groupId);
        fields = WhereEquals(fields, field => field.AppliesToCategoryId, appliesToCategoryId);
        fields = WhereEquals(fields, field => field.ComponentType, componentType);

        var filtered = fields.OrderBy(field => field.CreatedAt).ThenBy(field => field.FieldName, StringComparer.Ordinal).ToList();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var names = await NameLookup.LoadAsync(groupRepository, categoryRepository, cancellationToken);
        return Results.Ok(new AssetCustomFieldListResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(names.ToResponse).ToList(),
            filtered.Count,
            page,
            pageSize));
    }

    private static async Task<IResult> GetFieldByIdAsync(
        string id,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        var field = await repository.GetByIdAsync(id, cancellationToken);
        return field is null || field.IsDeleted
            ? NotFound(id)
            : Results.Ok((await NameLookup.LoadAsync(groupRepository, categoryRepository, cancellationToken)).ToResponse(field));
    }

    private static async Task<IResult> CreateFieldAsync(
        SaveAssetCustomFieldRequest request,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var field = request.ToEntity(currentUserAccessor.GetCurrentUser());

        var problem = await CheckLinksAndNameAsync(field, null, repository, groupRepository, categoryRepository, cancellationToken);
        if (problem is not null)
        {
            return problem.ToResult();
        }

        if (field.GroupId is not null)
        {
            field.DisplayOrder = await repository.NextDisplayOrderAsync(field.GroupId, cancellationToken);
        }

        await repository.CreateAsync(field, cancellationToken);
        await eventLogger.LogAsync(EntityType, field.FieldName, field.DisplayLabel, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-custom-fields/{field.Id}",
            (await NameLookup.LoadAsync(groupRepository, categoryRepository, cancellationToken)).ToResponse(field));
    }

    private static async Task<IResult> UpdateFieldAsync(
        string id,
        SaveAssetCustomFieldRequest request,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        IAssetFieldLayoutRepository layoutRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var field = await repository.GetByIdAsync(id, cancellationToken);
        if (field is null || field.IsDeleted)
        {
            return NotFound(id);
        }

        var problem = await UpdateExistingAsync(field, request, repository, groupRepository, categoryRepository, layoutRepository, currentUserAccessor.GetCurrentUser(), cancellationToken);
        if (problem is not null)
        {
            return problem.ToResult();
        }

        await eventLogger.LogAsync(EntityType, field.FieldName, field.DisplayLabel, EventAction.Updated, cancellationToken);

        return Results.Ok((await NameLookup.LoadAsync(groupRepository, categoryRepository, cancellationToken)).ToResponse(field));
    }

    private static async Task<IResult> DeleteFieldAsync(
        string id,
        IAssetCustomFieldRepository repository,
        IAssetFieldLayoutRepository layoutRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var field = await repository.GetByIdAsync(id, cancellationToken);
        if (field is null || field.IsDeleted)
        {
            return NotFound(id);
        }

        if (field.Scope != CustomFieldScopes.Component
            && await layoutRepository.GetUsingFieldAsync(field.FieldName, cancellationToken) is { Count: > 0 } layouts)
        {
            return Results.Conflict(new
            {
                message = $"Field '{field.FieldName}' is used in layouts: {string.Join(", ", layouts.Select(layout => layout.LayoutName))}. Remove it from them first, or set the field to Inactive."
            });
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, field.FieldName, field.DisplayLabel, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ExportFieldsAsync(
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken,
        string? scope = null)
    {
        var scopes = CustomFieldScopes.Normalize(scope) is { } listedScope ? [listedScope] : CustomFieldScopes.All.ToList();
        var fields = await repository.GetByScopesAsync(scopes, cancellationToken);

        var groupNames = (await groupRepository.GetAllAsync(cancellationToken)).ToDictionary(group => group.Id, group => group.GroupName);
        var categoryCodes = (await categoryRepository.GetAllAsync(cancellationToken)).ToDictionary(category => category.Id, category => category.Code);

        return Results.Ok(fields
            .OrderBy(field => field.Scope)
            .ThenBy(field => field.CreatedAt)
            .Select(field => new AssetCustomFieldExportItem(
                field.Scope,
                field.FieldName,
                field.DisplayLabel,
                field.FieldType,
                field.GroupId is not null ? groupNames.GetValueOrDefault(field.GroupId) : null,
                field.AppliesToCategoryId is not null ? categoryCodes.GetValueOrDefault(field.AppliesToCategoryId) : null,
                field.ComponentType,
                field.Required,
                field.ShowInDetails,
                field.IncludeInSearch,
                field.Status,
                field.DefaultValue,
                field.Description,
                field.HelpText,
                field.Options,
                field.CompoundParts.Select(part => new CustomFieldPartDto(part.Key, part.Label, part.Unit)).ToList(),
                field.LookupSource))
            .ToList());
    }

    /// <summary>
    /// Imports a file produced by export. Fields are matched by name: new names are created, existing ones updated.
    /// Groups named in the file are created when missing; categories are matched by code. Each item stands alone,
    /// so one bad item is reported without stopping the rest.
    /// </summary>
    private static async Task<IResult> ImportFieldsAsync(
        List<AssetCustomFieldExportItem> items,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        IAssetFieldLayoutRepository layoutRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken,
        string? scope = null)
    {
        var defaultScope = CustomFieldScopes.Normalize(scope);
        var categories = (await categoryRepository.GetAllAsync(cancellationToken)).Where(category => !category.IsDeleted).ToList();
        var currentUser = currentUserAccessor.GetCurrentUser();

        var created = 0;
        var updated = 0;
        var errors = new List<ImportItemError>();

        foreach (var item in items)
        {
            var itemScope = CustomFieldScopes.Normalize(item.Scope) ?? defaultScope;

            string? categoryId = null;
            if (!string.IsNullOrWhiteSpace(item.AppliesToCategoryCode))
            {
                categoryId = categories.FirstOrDefault(category => string.Equals(category.Code, item.AppliesToCategoryCode.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
                if (categoryId is null)
                {
                    errors.Add(new ImportItemError(item.FieldName, $"Asset category with code '{item.AppliesToCategoryCode.Trim()}' was not found"));
                    continue;
                }
            }

            string? groupId = null;
            if (!string.IsNullOrWhiteSpace(item.GroupName))
            {
                groupId = (await GetOrCreateGroupAsync(item.GroupName.Trim(), groupRepository, currentUser, cancellationToken)).Id;
            }

            var request = new SaveAssetCustomFieldRequest(
                itemScope, item.FieldName, item.DisplayLabel, item.FieldType, groupId, categoryId, item.ComponentType,
                item.Required, item.ShowInDetails, item.IncludeInSearch, item.Status, item.DefaultValue, item.Description,
                item.HelpText, item.Options, item.CompoundParts, item.LookupSource, null, null, null);

            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                errors.Add(new ImportItemError(item.FieldName, string.Join("; ", validationErrors.SelectMany(error => error.Value))));
                continue;
            }

            var fieldName = item.FieldName!.Trim();
            var existing = (await repository.GetByScopesAsync(CustomFieldScopes.SameNamespaceAs(itemScope!), cancellationToken))
                .FirstOrDefault(field => field.FieldName == fieldName);

            FieldProblem? problem;
            if (existing is null)
            {
                var field = request.ToEntity(currentUser);
                problem = await CheckLinksAndNameAsync(field, null, repository, groupRepository, categoryRepository, cancellationToken);
                if (problem is null)
                {
                    if (field.GroupId is not null)
                    {
                        field.DisplayOrder = await repository.NextDisplayOrderAsync(field.GroupId, cancellationToken);
                    }

                    await repository.CreateAsync(field, cancellationToken);
                    await eventLogger.LogAsync(EntityType, field.FieldName, field.DisplayLabel, EventAction.Created, cancellationToken);
                    created++;
                    continue;
                }
            }
            else
            {
                problem = await UpdateExistingAsync(existing, request, repository, groupRepository, categoryRepository, layoutRepository, currentUser, cancellationToken);
                if (problem is null)
                {
                    await eventLogger.LogAsync(EntityType, existing.FieldName, existing.DisplayLabel, EventAction.Updated, cancellationToken);
                    updated++;
                    continue;
                }
            }

            errors.Add(new ImportItemError(item.FieldName, problem.Message));
        }

        return Results.Ok(new ImportResult(created, updated, errors));
    }

    /// <summary>Applies an update to a stored field; returns the problem, or null once it is saved.</summary>
    private static async Task<FieldProblem?> UpdateExistingAsync(
        AssetCustomFieldDefinition field,
        SaveAssetCustomFieldRequest request,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        IAssetFieldLayoutRepository layoutRepository,
        CurrentUser? currentUser,
        CancellationToken cancellationToken)
    {
        if (CustomFieldScopes.Normalize(request.Scope) != field.Scope)
        {
            return FieldProblem.Invalid("Scope", $"Scope can't be changed once a field is created (it is {field.Scope})");
        }

        if (CustomFieldTypes.Normalize(request.FieldType) != field.FieldType)
        {
            return FieldProblem.Invalid("FieldType", $"Field type can't be changed once a field is created (it is {field.FieldType})");
        }

        var oldFieldName = field.FieldName;
        var oldGroupId = field.GroupId;

        request.ApplyTo(field, currentUser);

        var problem = await CheckLinksAndNameAsync(field, field.Id, repository, groupRepository, categoryRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (field.GroupId != oldGroupId)
        {
            field.DisplayOrder = field.GroupId is null ? 0 : await repository.NextDisplayOrderAsync(field.GroupId, cancellationToken);
        }

        if (!await repository.UpdateAsync(field.Id, field, cancellationToken))
        {
            return FieldProblem.Missing($"Custom field '{field.Id}' was not found.");
        }

        // Layouts refer to fields by name, so a rename is carried into them.
        if (field.FieldName != oldFieldName && field.Scope != CustomFieldScopes.Component)
        {
            await layoutRepository.RenameFieldAsync(oldFieldName, field.FieldName, cancellationToken);
        }

        return null;
    }

    /// <param name="excludeId">The field being updated, so it does not clash with itself.</param>
    private static async Task<FieldProblem?> CheckLinksAndNameAsync(
        AssetCustomFieldDefinition field,
        string? excludeId,
        IAssetCustomFieldRepository repository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        if (field.GroupId is { } groupId
            && (!MongoObjectId.IsValid(groupId) || await groupRepository.GetByIdAsync(groupId, cancellationToken) is not { IsDeleted: false }))
        {
            return FieldProblem.Invalid("GroupId", $"Field group '{groupId}' was not found");
        }

        if (field.AppliesToCategoryId is { } categoryId
            && (!MongoObjectId.IsValid(categoryId) || await categoryRepository.GetByIdAsync(categoryId, cancellationToken) is not { IsDeleted: false }))
        {
            return FieldProblem.Invalid("AppliesToCategoryId", $"Asset category '{categoryId}' was not found");
        }

        var clash = (await repository.GetByScopesAsync(CustomFieldScopes.SameNamespaceAs(field.Scope), cancellationToken))
            .FirstOrDefault(other => other.FieldName == field.FieldName && other.Id != excludeId);
        if (clash is not null)
        {
            return FieldProblem.Conflict($"A field named '{field.FieldName}' already exists ({clash.Scope} field '{clash.DisplayLabel}').");
        }

        return null;
    }

    private static async Task<AssetFieldGroup> GetOrCreateGroupAsync(
        string groupName,
        IAssetFieldGroupRepository groupRepository,
        CurrentUser? currentUser,
        CancellationToken cancellationToken)
    {
        return await groupRepository.GetByNameAsync(groupName, cancellationToken)
            ?? await groupRepository.CreateAsync(
                new AssetFieldGroup
                {
                    GroupName = groupName,
                    Status = ActiveInactiveStatuses.Active,
                    CreatedBy = currentUser?.UserName,
                    CreatedAt = DateTime.UtcNow,
                    ClientId = currentUser?.ClientId,
                    TenantId = currentUser?.TenantId
                },
                cancellationToken);
    }

    private static IEnumerable<AssetCustomFieldDefinition> WhereEquals(
        IEnumerable<AssetCustomFieldDefinition> fields,
        Func<AssetCustomFieldDefinition, string?> selector,
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fields
            : fields.Where(field => string.Equals(selector(field), value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Custom field '{id}' was not found." });
    }

    /// <summary>Why a field could not be saved; becomes an HTTP result for the API, or a message in an import report.</summary>
    private sealed record FieldProblem(int StatusCode, string? Field, string Message)
    {
        public static FieldProblem Invalid(string field, string message) => new(StatusCodes.Status400BadRequest, field, message);

        public static FieldProblem Conflict(string message) => new(StatusCodes.Status409Conflict, null, message);

        public static FieldProblem Missing(string message) => new(StatusCodes.Status404NotFound, null, message);

        public IResult ToResult() => StatusCode switch
        {
            StatusCodes.Status400BadRequest => Results.ValidationProblem(new Dictionary<string, string[]> { [Field!] = [Message] }),
            StatusCodes.Status409Conflict => Results.Conflict(new { message = Message }),
            _ => Results.NotFound(new { message = Message })
        };
    }

    /// <summary>Resolves group and category names for responses from one load of each.</summary>
    private sealed class NameLookup(IReadOnlyDictionary<string, string> groupNames, IReadOnlyDictionary<string, string> categoryNames)
    {
        public static async Task<NameLookup> LoadAsync(
            IAssetFieldGroupRepository groupRepository,
            IAssetCategoryRepository categoryRepository,
            CancellationToken cancellationToken)
        {
            return new NameLookup(
                (await groupRepository.GetAllAsync(cancellationToken)).ToDictionary(group => group.Id, group => group.GroupName),
                (await categoryRepository.GetAllAsync(cancellationToken)).ToDictionary(category => category.Id, category => category.Name));
        }

        public AssetCustomFieldResponse ToResponse(AssetCustomFieldDefinition field)
        {
            return AssetCustomFieldResponse.FromEntity(
                field,
                field.GroupId is not null ? groupNames.GetValueOrDefault(field.GroupId) : null,
                field.AppliesToCategoryId is not null ? categoryNames.GetValueOrDefault(field.AppliesToCategoryId) : null);
        }
    }
}
