using System.Text;
using A360.Asset.Api.Contracts;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Endpoints;

/// <summary>Custom Field Values page: the custom fields that apply to one asset, with the values recorded for it.</summary>
public static class AssetCustomFieldValueEndpoints
{
    public static RouteGroupBuilder MapAssetCustomFieldValueEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations/{assetRegistrationId}/custom-fields").WithTags("Asset Custom Fields");

        group.MapGet("", GetValuesAsync).WithName("GetAssetCustomFieldValues");
        group.MapGet("/export", ExportValuesAsync).WithName("ExportAssetCustomFieldValues");

        return group;
    }

    /// <param name="emptyOnly">"Show empty only": list only fields with no value (the counts still cover every field).</param>
    private static async Task<IResult> GetValuesAsync(
        string assetRegistrationId,
        IAssetRegistrationRepository assetRepository,
        IAssetCustomFieldRepository fieldRepository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken,
        bool emptyOnly = false)
    {
        var result = await LoadAsync(assetRegistrationId, emptyOnly, assetRepository, fieldRepository, groupRepository, categoryRepository, cancellationToken);
        return result is null ? AssetNotFound(assetRegistrationId) : Results.Ok(result);
    }

    /// <summary>Export CSV: one row per field with its group, value and whether it is required.</summary>
    private static async Task<IResult> ExportValuesAsync(
        string assetRegistrationId,
        IAssetRegistrationRepository assetRepository,
        IAssetCustomFieldRepository fieldRepository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken,
        bool emptyOnly = false)
    {
        var result = await LoadAsync(assetRegistrationId, emptyOnly, assetRepository, fieldRepository, groupRepository, categoryRepository, cancellationToken);
        if (result is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var csv = new StringBuilder();
        csv.AppendLine("Group,Field,Field Type,Value,Required,Status");
        foreach (var group in result.Groups)
        {
            foreach (var field in group.Fields)
            {
                var status = field.IsMissing ? "Missing" : field.IsFilled ? "Filled" : "Not set";
                csv.AppendLine(string.Join(',',
                    Csv(group.GroupName), Csv(field.Label), Csv(field.FieldType), Csv(field.Value),
                    field.Required ? "Yes" : "No", status));
            }
        }

        var name = $"{result.AssetTag ?? result.AssetRegistrationId}-custom-fields.csv";
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", name);
    }

    private static async Task<AssetCustomFieldValuesResponse?> LoadAsync(
        string assetRegistrationId,
        bool emptyOnly,
        IAssetRegistrationRepository assetRepository,
        IAssetCustomFieldRepository fieldRepository,
        IAssetFieldGroupRepository groupRepository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var definitions = await fieldRepository.GetByScopesAsync([CustomFieldScopes.Asset, CustomFieldScopes.Category], cancellationToken);
        var groups = (await groupRepository.GetAllAsync(cancellationToken)).Where(group => !group.IsDeleted).ToList();
        var categories = await categoryRepository.GetAllAsync(cancellationToken);

        return AssetCustomFieldValues.Build(asset, definitions.Where(definition => !definition.IsDeleted).ToList(), groups, categories, emptyOnly);
    }

    /// <summary>Quotes a CSV cell when needed, and stops spreadsheet apps reading it as a formula.</summary>
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@".Contains(text[0]))
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static IResult AssetNotFound(string assetRegistrationId) =>
        Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });
}
