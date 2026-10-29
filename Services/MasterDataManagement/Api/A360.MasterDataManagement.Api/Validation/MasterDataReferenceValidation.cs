using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class MasterDataReferenceValidation
{
    /// <summary>
    /// Every entry needs a name (or just an id when <paramref name="idResolvesName"/> is set, because the name
    /// is then filled in from that master), and the same entry may appear only once.
    /// </summary>
    public static void ValidateList(
        Dictionary<string, string[]> errors,
        string fieldName,
        List<MasterDataReferenceDto>? references,
        bool idResolvesName = false)
    {
        if (references is null || references.Count == 0)
        {
            return;
        }

        if (references.Any(x => x is null
            || (string.IsNullOrWhiteSpace(x.Name) && !(idResolvesName && !string.IsNullOrWhiteSpace(x.Id)))))
        {
            errors[fieldName] =
                [idResolvesName ? $"Every entry in {fieldName} needs an id or a name" : $"Every entry in {fieldName} needs a name"];
            return;
        }

        var hasDuplicates = references
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Id) ? x.Name!.Trim().ToUpperInvariant() : x.Id.Trim())
            .Any(x => x.Count() > 1);

        if (hasDuplicates)
        {
            errors[fieldName] =
                [$"{fieldName} contains duplicate entries"];
        }
    }
}
