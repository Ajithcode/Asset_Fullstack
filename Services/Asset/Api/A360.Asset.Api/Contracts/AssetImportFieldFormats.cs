using System.Globalization;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// The value format and an example value for each import field, shown next to the field in Field Mapping
/// so users know what to type in the file (e.g. Purchase Date → DD/MM/YYYY, 10/01/2023).
/// </summary>
public static class AssetImportFieldFormats
{
    /// <summary>The date format users are asked to use. Other common formats (2025-01-15, 15-Jan-2025, Excel dates) are still read.</summary>
    public const string DateFormat = "DD/MM/YYYY";

    private const string DateExample = "15/01/2025";

    /// <summary>Example values from the first sample asset, keyed by column label.</summary>
    private static readonly Dictionary<string, string> SampleByLabel = BuildSamples();

    public static string Format(AssetImportField field) => field.DataType switch
    {
        AssetImportDataTypes.Date => DateFormat,
        AssetImportDataTypes.Number => "Number (digits, decimal point allowed, no currency symbol)",
        AssetImportDataTypes.Integer => "Whole number",
        AssetImportDataTypes.Lookup when field.Options is { } options && options.All.Any() => $"One of: {string.Join(", ", options.All)}",
        AssetImportDataTypes.Lookup => "Text (must match a value set up in Asset360)",
        _ => "Text"
    };

    public static string? Example(AssetImportField field)
    {
        if (field.Custom is null && SampleByLabel.TryGetValue(field.Label, out var sample))
        {
            return sample;
        }

        return field.DataType switch
        {
            AssetImportDataTypes.Date => DateExample,
            AssetImportDataTypes.Number => "1500.50",
            AssetImportDataTypes.Integer => "3",
            AssetImportDataTypes.Lookup => field.Options?.All.FirstOrDefault(),
            _ when field.Key == AssetImportFields.AssetId => "AR000123",
            _ => null
        };
    }

    public static string FormatValue(object? value) => value switch
    {
        DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static Dictionary<string, string> BuildSamples()
    {
        var samples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headers = AssetImportColumns.All.Select(column => column.Header).ToList();

        // First sample row that has a value for the column.
        for (var index = 0; index < headers.Count; index++)
        {
            var value = AssetImportSampleData.Rows
                .Select(row => index < row.Length ? row[index] : null)
                .FirstOrDefault(cell => cell is not null);
            if (value is not null)
            {
                samples[headers[index]] = FormatValue(value);
            }
        }

        return samples;
    }
}
