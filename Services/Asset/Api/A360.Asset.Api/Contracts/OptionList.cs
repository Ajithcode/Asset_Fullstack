namespace A360.Asset.Api.Contracts;

/// <summary>A fixed list of choices for a dropdown or radio group, matched ignoring case.</summary>
public sealed class OptionList(params string[] values)
{
    public IReadOnlyList<string> All { get; } = values;

    public bool IsValid(string? value) => Normalize(value) is not null;

    /// <summary>Standard spelling, e.g. "ad-hoc" as "Ad-hoc"; null when not recognised.</summary>
    public string? Normalize(string? value) =>
        All.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The standard spelling, or the trimmed text when not recognised (so validation can report it).</summary>
    public string? NormalizeOrKeep(string? value) => Normalize(value) ?? ContractText.TrimOrNull(value);
}
