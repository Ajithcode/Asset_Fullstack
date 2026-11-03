using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Field layouts (the Field Layouts tab): sections of fields, in order, for an asset type or for all assets.</summary>
public static class AssetFieldLayoutEndpoints
{
    private const string EntityType = "AssetFieldLayout";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAssetFieldLayoutEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-field-layouts").WithTags("Asset Field Layouts");

        group.MapGet("", GetLayoutsAsync).WithName("GetAssetFieldLayouts");
        group.MapGet("/available-fields", GetAvailableFieldsAsync).WithName("GetAssetFieldLayoutAvailableFields");
        group.MapGet("/{id}", GetLayoutByIdAsync).WithName("GetAssetFieldLayoutById");
        group.MapPost("", CreateLayoutAsync).WithName("CreateAssetFieldLayout");
        group.MapPut("/{id}", UpdateLayoutAsync).WithName("UpdateAssetFieldLayout");
        group.MapDelete("/{id}", DeleteLayoutAsync).WithName("DeleteAssetFieldLayout");

        return group;
    }

    private static async Task<IResult> GetLayoutsAsync(
        IAssetFieldLayoutRepository repository,
        LayoutContextLoader loader,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetTypeId = null,
        string? status = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var layouts = (await repository.GetAllAsync(cancellationToken)).Where(layout => !layout.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            layouts = layouts.Where(layout =>
                layout.LayoutName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (layout.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // "all" lists the layouts for all assets; an id lists that asset type's layouts.
        if (!string.IsNullOrWhiteSpace(assetTypeId))
        {
            var typeId = assetTypeId.Trim();
            layouts = string.Equals(typeId, "all", StringComparison.OrdinalIgnoreCase)
                ? layouts.Where(layout => layout.AssetTypeId is null)
                : layouts.Where(layout => layout.AssetTypeId == typeId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            layouts = layouts.Where(layout => string.Equals(layout.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var filtered = layouts.OrderBy(layout => layout.CreatedAt).ToList();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var context = await loader.LoadAsync(cancellationToken);
        return Results.Ok(new AssetFieldLayoutListResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(context.ToResponse).ToList(),
            filtered.Count,
            page,
            pageSize));
    }

    /// <summary>
    /// The fields a layout for this asset type can use: built-in asset fields, active asset fields, and active
    /// category fields for the type's category or any category above it. Without an asset type, category fields are left out.
    /// </summary>
    private static async Task<IResult> GetAvailableFieldsAsync(
        LayoutContextLoader loader,
        CancellationToken cancellationToken,
        string? assetTypeId = null)
    {
        var context = await loader.LoadAsync(cancellationToken);

        AssetCategoryType? type = null;
        if (!string.IsNullOrWhiteSpace(assetTypeId) && !context.TypesById.TryGetValue(assetTypeId.Trim(), out type))
        {
            return Results.NotFound(new { message = $"Asset type '{assetTypeId.Trim()}' was not found." });
        }

        return Results.Ok(context.AvailableFields(type)
            .Where(field => field.Status != ActiveInactiveStatuses.Inactive)
            .Select(field => field.Response)
            .ToList());
    }

    private static async Task<IResult> GetLayoutByIdAsync(
        string id,
        IAssetFieldLayoutRepository repository,
        LayoutContextLoader loader,
        CancellationToken cancellationToken)
    {
        var layout = await repository.GetByIdAsync(id, cancellationToken);
        return layout is null || layout.IsDeleted
            ? NotFound(id)
            : Results.Ok((await loader.LoadAsync(cancellationToken)).ToResponse(layout));
    }

    private static async Task<IResult> CreateLayoutAsync(
        SaveAssetFieldLayoutRequest request,
        IAssetFieldLayoutRepository repository,
        LayoutContextLoader loader,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var layout = new AssetFieldLayout
        {
            CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        var context = await loader.LoadAsync(cancellationToken);
        var problem = await ApplyAndCheckAsync(layout, request, null, repository, context, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(layout, cancellationToken);
        await eventLogger.LogAsync(EntityType, layout.Id, layout.LayoutName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-field-layouts/{layout.Id}", context.ToResponse(layout));
    }

    private static async Task<IResult> UpdateLayoutAsync(
        string id,
        SaveAssetFieldLayoutRequest request,
        IAssetFieldLayoutRepository repository,
        LayoutContextLoader loader,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var layout = await repository.GetByIdAsync(id, cancellationToken);
        if (layout is null || layout.IsDeleted)
        {
            return NotFound(id);
        }

        var context = await loader.LoadAsync(cancellationToken);
        var problem = await ApplyAndCheckAsync(layout, request, id, repository, context, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        layout.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        layout.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(id, layout, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, layout.Id, layout.LayoutName, EventAction.Updated, cancellationToken);

        return Results.Ok(context.ToResponse(layout));
    }

    private static async Task<IResult> DeleteLayoutAsync(
        string id,
        IAssetFieldLayoutRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var layout = await repository.GetByIdAsync(id, cancellationToken);
        if (layout is null || layout.IsDeleted)
        {
            return NotFound(id);
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, layout.Id, layout.LayoutName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Copies the request onto the layout and checks the asset type, groups and fields it refers to.</summary>
    /// <param name="excludeId">The layout being updated, so it does not clash with itself.</param>
    private static async Task<IResult?> ApplyAndCheckAsync(
        AssetFieldLayout layout,
        SaveAssetFieldLayoutRequest request,
        string? excludeId,
        IAssetFieldLayoutRepository repository,
        LayoutContext context,
        CancellationToken cancellationToken)
    {
        layout.LayoutName = request.LayoutName!.Trim();
        layout.Description = ContractText.TrimOrNull(request.Description);
        layout.Status = ActiveInactiveStatuses.Normalize(request.Status);
        layout.AssetTypeId = ContractText.TrimOrNull(request.AssetTypeId);
        layout.Sections = (request.Sections ?? [])
            .Select(section => new AssetFieldLayoutSection
            {
                GroupId = section.GroupId!.Trim(),
                FieldNames = (section.FieldNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList()
            })
            .ToList();

        var errors = new Dictionary<string, string[]>();

        AssetCategoryType? type = null;
        if (layout.AssetTypeId is { } typeId && !context.TypesById.TryGetValue(typeId, out type))
        {
            errors["AssetTypeId"] = [$"Asset type '{typeId}' was not found"];
        }

        layout.AssetTypeName = type?.TypeName;

        var unknownGroups = layout.Sections.Select(section => section.GroupId).Where(groupId => !context.GroupNames.ContainsKey(groupId)).ToList();
        if (unknownGroups.Count > 0)
        {
            errors["Sections.GroupId"] = [$"Field groups not found: {string.Join(", ", unknownGroups)}"];
        }

        if (!errors.ContainsKey("AssetTypeId"))
        {
            var available = context.AvailableFields(type).Select(field => field.Response.FieldName).ToHashSet();
            var unknownFields = layout.Sections.SelectMany(section => section.FieldNames).Where(name => !available.Contains(name)).ToList();
            if (unknownFields.Count > 0)
            {
                errors["Sections.FieldNames"] = [$"These fields are not available for this layout's asset type: {string.Join(", ", unknownFields)}"];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await repository.NameExistsAsync(layout.LayoutName, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"A layout named '{layout.LayoutName}' already exists." });
        }

        return null;
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Field layout '{id}' was not found." });
    }

    /// <summary>Loads everything a layout refers to (asset types, categories, groups, fields) once per request.</summary>
    public sealed class LayoutContextLoader(
        IAssetCategoryTypeRepository typeRepository,
        IAssetCategoryRepository categoryRepository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCustomFieldRepository fieldRepository)
    {
        public async Task<LayoutContext> LoadAsync(CancellationToken cancellationToken)
        {
            return new LayoutContext(
                (await typeRepository.GetAllAsync(cancellationToken)).Where(type => !type.IsDeleted).ToDictionary(type => type.Id),
                (await categoryRepository.GetAllAsync(cancellationToken)).Where(category => !category.IsDeleted).ToDictionary(category => category.Id),
                (await groupRepository.GetAllAsync(cancellationToken)).Where(group => !group.IsDeleted).ToDictionary(group => group.Id, group => group.GroupName),
                await fieldRepository.GetByScopesAsync([CustomFieldScopes.Asset, CustomFieldScopes.Category], cancellationToken));
        }
    }

    public sealed record AvailableField(AvailableLayoutFieldResponse Response, string? Status);

    public sealed class LayoutContext(
        IReadOnlyDictionary<string, AssetCategoryType> typesById,
        IReadOnlyDictionary<string, AssetCategory> categoriesById,
        IReadOnlyDictionary<string, string> groupNames,
        IReadOnlyCollection<AssetCustomFieldDefinition> fields)
    {
        public IReadOnlyDictionary<string, AssetCategoryType> TypesById => typesById;

        public IReadOnlyDictionary<string, string> GroupNames => groupNames;

        /// <summary>Built-in fields, asset fields, and category fields for the type's category or any category above it.</summary>
        public IReadOnlyList<AvailableField> AvailableFields(AssetCategoryType? type)
        {
            var categoryIds = type is not null && categoriesById.TryGetValue(type.CategoryId, out var category)
                ? AssetCategoryLineage.SelfAndAncestors(category, categoriesById).Select(lineage => lineage.Id).ToHashSet()
                : [];

            return BuiltInAssetFields.All
                .Select(field => new AvailableField(field, ActiveInactiveStatuses.Active))
                .Concat(fields
                    .Where(field => field.Scope == CustomFieldScopes.Asset
                                    || (field.AppliesToCategoryId is not null && categoryIds.Contains(field.AppliesToCategoryId)))
                    .OrderBy(field => field.CreatedAt)
                    .Select(field => new AvailableField(
                        new AvailableLayoutFieldResponse(field.FieldName, field.DisplayLabel, field.FieldType, field.Required, field.Scope),
                        field.Status)))
                .ToList();
        }

        public AssetFieldLayoutResponse ToResponse(AssetFieldLayout layout)
        {
            var fieldsByName = BuiltInAssetFields.All
                .Concat(fields.Select(field => new AvailableLayoutFieldResponse(field.FieldName, field.DisplayLabel, field.FieldType, field.Required, field.Scope)))
                .GroupBy(field => field.FieldName)
                .ToDictionary(group => group.Key, group => group.First());

            var sections = layout.Sections
                .Select(section => new AssetFieldLayoutSectionResponse(
                    section.GroupId,
                    groupNames.GetValueOrDefault(section.GroupId),
                    section.FieldNames.Where(fieldsByName.ContainsKey).Select(name => fieldsByName[name]).ToList()))
                .ToList();

            return new AssetFieldLayoutResponse(
                layout.Id,
                layout.LayoutName,
                layout.AssetTypeId,
                layout.AssetTypeId is null ? AssetFieldLayoutResponse.AllAssets : layout.AssetTypeName ?? layout.AssetTypeId,
                layout.Description,
                layout.Status,
                sections.Count,
                sections.Sum(section => section.Fields.Count),
                sections,
                layout.CreatedBy,
                layout.CreatedAt,
                layout.UpdatedBy,
                layout.UpdatedAt);
        }
    }
}
