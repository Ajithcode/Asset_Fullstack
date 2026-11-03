using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetRelationshipValidationExtensions
{
    private const int MaxMainAssets = 100;
    private const int MaxSubAssets = 200;

    public static Dictionary<string, string[]> Validate(this SaveAssetRelationshipsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var relationships = request.Relationships ?? [];
        if (relationships.Count == 0)
        {
            errors["Relationships"] = ["Select at least one asset"];
            return errors;
        }

        if (relationships.Count > MaxMainAssets)
        {
            errors["Relationships"] = [$"At most {MaxMainAssets} assets can be saved at once"];
            return errors;
        }

        var duplicates = relationships
            .Where(item => !string.IsNullOrWhiteSpace(item.MainAssetRegistrationId))
            .GroupBy(item => item.MainAssetRegistrationId!.Trim(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            errors["Relationships"] = [$"Each asset can be sent only once: {string.Join(", ", duplicates)}"];
        }

        var index = 0;
        foreach (var item in relationships)
        {
            var prefix = $"Relationships[{index}]";
            if (string.IsNullOrWhiteSpace(item.MainAssetRegistrationId))
            {
                errors[$"{prefix}.MainAssetRegistrationId"] = ["MainAssetRegistrationId is required"];
            }

            ValidateSubAssets(errors, $"{prefix}.SubAssets", item.MainAssetRegistrationId, item.SubAssets);
            index++;
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetRelationshipRequest request, string mainAssetRegistrationId)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateSubAssets(errors, "SubAssets", mainAssetRegistrationId, request.SubAssets);
        return errors;
    }

    private static void ValidateSubAssets(
        Dictionary<string, string[]> errors,
        string prefix,
        string? mainAssetRegistrationId,
        IReadOnlyCollection<AssetRelationshipSubAssetRequest>? subAssets)
    {
        var items = subAssets ?? [];
        if (items.Count == 0)
        {
            errors[prefix] = ["Select at least one sub asset"];
            return;
        }

        if (items.Count > MaxSubAssets)
        {
            errors[prefix] = [$"An asset can have at most {MaxSubAssets} sub assets"];
            return;
        }

        var index = 0;
        foreach (var subAsset in items)
        {
            var itemPrefix = $"{prefix}[{index}]";
            var hasAsset = !string.IsNullOrWhiteSpace(subAsset.AssetRegistrationId);
            var hasComponent = !string.IsNullOrWhiteSpace(subAsset.ComponentId);

            if (hasAsset == hasComponent)
            {
                errors[itemPrefix] = ["Send either AssetRegistrationId or ComponentId"];
            }
            else if (hasAsset && string.Equals(subAsset.AssetRegistrationId!.Trim(), mainAssetRegistrationId?.Trim(), StringComparison.Ordinal))
            {
                errors[$"{itemPrefix}.AssetRegistrationId"] = ["An asset cannot be a sub asset of itself"];
            }

            if (!string.IsNullOrWhiteSpace(subAsset.RelationshipType) && !AssetRelationshipTypes.IsValid(subAsset.RelationshipType))
            {
                errors[$"{itemPrefix}.RelationshipType"] = [$"RelationshipType must be one of: {string.Join(", ", AssetRelationshipTypes.All)}"];
            }

            index++;
        }
    }
}
