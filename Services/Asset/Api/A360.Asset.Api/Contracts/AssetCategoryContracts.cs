using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

public sealed record CreateAssetCategoryRequest(
    string? ParentCategoryId,
    string? Name,
    string? Code,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetCategory ToEntity(CurrentUser? currentUser)
    {
        return new AssetCategory
        {
            ParentCategoryId = ContractText.TrimOrNull(ParentCategoryId),
            Name = Name!.Trim(),
            Code = Code!.Trim(),
            Description = ContractText.TrimOrNull(Description),
            Status = ActiveInactiveStatuses.Normalize(Status),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetCategoryRequest(
    string? ParentCategoryId,
    string? Name,
    string? Code,
    string? Description,
    string? Status,
    string? ImageUrl)
{
    public void ApplyTo(AssetCategory category, CurrentUser? currentUser)
    {
        category.ParentCategoryId = ContractText.TrimOrNull(ParentCategoryId);
        category.Name = Name!.Trim();
        category.Code = Code!.Trim();
        category.Description = ContractText.TrimOrNull(Description);
        category.Status = ActiveInactiveStatuses.Normalize(Status);
        category.ImageUrl = ContractText.TrimOrNull(ImageUrl);
        category.UpdatedBy = currentUser?.UserName;
        category.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record AssetCategoryResponse(
    string Id,
    string? ParentCategoryId,
    string? ParentCategoryName,
    string Name,
    string Code,
    string? Description,
    string? Status,
    string? ImageUrl,
    IReadOnlyList<AssetCategoryCustomFieldResponse> CustomFields,
    IReadOnlyList<AssetCategoryAttributeResponse> Attributes,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    /// <param name="categoriesById">All categories, to resolve the parent's name and the inherited fields and attributes.</param>
    public static AssetCategoryResponse FromEntity(AssetCategory category, IReadOnlyDictionary<string, AssetCategory> categoriesById)
    {
        // Ancestors first (top-level down), then the category itself, so inherited items are listed before its own.
        var lineage = AssetCategoryLineage.SelfAndAncestors(category, categoriesById).Reverse().ToList();

        return new AssetCategoryResponse(
            category.Id,
            category.ParentCategoryId,
            category.ParentCategoryId is not null && categoriesById.TryGetValue(category.ParentCategoryId, out var parent) ? parent.Name : null,
            category.Name,
            category.Code,
            category.Description,
            category.Status,
            category.ImageUrl,
            lineage
                .SelectMany(owner => owner.CustomFields.Select(field => AssetCategoryCustomFieldResponse.FromEntity(field, owner, category)))
                .ToList(),
            lineage
                .SelectMany(owner => owner.Attributes.Select(attribute => AssetCategoryAttributeResponse.FromEntity(attribute, owner, category)))
                .ToList(),
            category.CreatedBy,
            category.CreatedAt,
            category.UpdatedBy,
            category.UpdatedAt,
            category.ClientId,
            category.TenantId);
    }
}

public static class ActiveInactiveStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static IEnumerable<string> All => [Active, Inactive];

    public static bool IsValid(string? status) =>
        string.Equals(status?.Trim(), Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status?.Trim(), Inactive, StringComparison.OrdinalIgnoreCase);

    /// <summary>New categories are Active unless a status is given; "inactive" is stored as "Inactive".</summary>
    public static string Normalize(string? status) =>
        string.Equals(status?.Trim(), Inactive, StringComparison.OrdinalIgnoreCase) ? Inactive : Active;
}

public sealed record CreateAssetCategoryCustomFieldRequest(
    string? FieldName,
    string? Type,
    bool? Required);

public sealed record CreateAssetCategoryAttributeRequest(
    string? Attribute,
    string? Unit,
    string? ExampleValue);

/// <param name="DefinedOnCategoryId">The category the field was created on; differs from the requested category when inherited.</param>
public sealed record AssetCategoryCustomFieldResponse(
    string Id,
    string FieldName,
    string Type,
    bool Required,
    string DefinedOnCategoryId,
    string DefinedOnCategoryName,
    bool Inherited,
    DateTime? CreatedAt)
{
    public static AssetCategoryCustomFieldResponse FromEntity(AssetCategoryCustomField field, AssetCategory definedOn, AssetCategory requested)
    {
        return new AssetCategoryCustomFieldResponse(
            field.Id,
            field.FieldName,
            field.Type,
            field.Required,
            definedOn.Id,
            definedOn.Name,
            definedOn.Id != requested.Id,
            field.CreatedAt);
    }
}

public sealed record AssetCategoryAttributeResponse(
    string Id,
    string Attribute,
    string? Unit,
    string? ExampleValue,
    string DefinedOnCategoryId,
    string DefinedOnCategoryName,
    bool Inherited,
    DateTime? CreatedAt)
{
    public static AssetCategoryAttributeResponse FromEntity(AssetCategoryAttribute attribute, AssetCategory definedOn, AssetCategory requested)
    {
        return new AssetCategoryAttributeResponse(
            attribute.Id,
            attribute.Attribute,
            attribute.Unit,
            attribute.ExampleValue,
            definedOn.Id,
            definedOn.Name,
            definedOn.Id != requested.Id,
            attribute.CreatedAt);
    }
}

/// <summary>Custom field types offered when defining a field.</summary>
public static class AssetCategoryCustomFieldTypes
{
    private static readonly string[] Types = ["Text", "Number", "Date", "Dropdown", "Checkbox"];

    public static IEnumerable<string> All => Types;

    /// <summary>Returns the type spelled as listed (e.g. "date" → "Date"), or null if it is not listed.</summary>
    public static string? Normalize(string? type) =>
        Types.FirstOrDefault(listed => string.Equals(listed, type?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class AssetCategoryLineage
{
    /// <summary>The category, its parent, its parent's parent and so on up to the top level.</summary>
    public static IEnumerable<AssetCategory> SelfAndAncestors(AssetCategory category, IReadOnlyDictionary<string, AssetCategory> categoriesById)
    {
        var visited = new HashSet<string>();
        AssetCategory? current = category;

        while (current is not null && visited.Add(current.Id))
        {
            yield return current;
            current = current.ParentCategoryId is not null && categoriesById.TryGetValue(current.ParentCategoryId, out var parent) ? parent : null;
        }
    }

    /// <summary>Every category below this one, at any depth.</summary>
    public static IEnumerable<AssetCategory> Descendants(AssetCategory category, IReadOnlyCollection<AssetCategory> categories)
    {
        var childrenByParent = categories
            .Where(child => child.ParentCategoryId is not null)
            .ToLookup(child => child.ParentCategoryId!);

        var visited = new HashSet<string> { category.Id };
        var pending = new Queue<AssetCategory>(childrenByParent[category.Id]);

        while (pending.Count > 0)
        {
            var child = pending.Dequeue();
            if (!visited.Add(child.Id))
            {
                continue;
            }

            yield return child;

            foreach (var grandchild in childrenByParent[child.Id])
            {
                pending.Enqueue(grandchild);
            }
        }
    }
}
