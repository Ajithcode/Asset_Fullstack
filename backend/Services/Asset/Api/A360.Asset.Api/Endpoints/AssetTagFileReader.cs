using System.Text;
using ClosedXML.Excel;
using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Reads asset tags (or serial numbers) from an uploaded .xlsx or .csv: the "Asset Tag" / "Serial Number" column,
/// or the first column when no header is recognised. Used by the Import from Excel buttons.
/// </summary>
internal static class AssetTagFileReader
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>The distinct tags in the file, or a message saying why the file cannot be used.</summary>
    public static async Task<(List<string> Tags, string? Error)> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ([], "Upload an Excel or CSV file with the asset tags");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return ([], "Only .xlsx and .csv files can be imported");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return ([], "File cannot be larger than 10 MB");
        }

        List<string> tags;
        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;
            tags = extension == ".csv" ? ReadTagsFromCsv(stream) : ReadTagsFromWorkbook(stream);
        }
        catch (Exception)
        {
            return ([], "The file could not be read");
        }

        tags = ContractText.CleanList(tags, StringComparer.Ordinal);
        return tags.Count == 0 ? ([], "The file has no asset tags") : (tags, null);
    }

    private static List<string> ReadTagsFromWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
        {
            return [];
        }

        var headers = sheet.Row(1).CellsUsed().Select(cell => (cell.Address.ColumnNumber, Text: cell.GetString())).ToList();
        var tagColumn = headers.FirstOrDefault(header => IsTagHeader(header.Text)).ColumnNumber;

        // Without a recognised header, the first column holds tags from row 1.
        var firstRow = tagColumn == 0 ? 1 : 2;
        tagColumn = tagColumn == 0 ? 1 : tagColumn;

        return Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
            .Select(row => sheet.Cell(row, tagColumn).GetString())
            .ToList();
    }

    private static List<string> ReadTagsFromCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = reader.ReadToEnd()
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .Select(line => line.Split(',', ';', '\t').Select(cell => cell.Trim().Trim('"')).ToArray())
            .ToList();
        if (lines.Count == 0)
        {
            return [];
        }

        var tagColumn = Array.FindIndex(lines[0], IsTagHeader);
        var rows = tagColumn < 0 ? lines : lines.Skip(1);
        tagColumn = Math.Max(0, tagColumn);

        return rows.Select(cells => tagColumn < cells.Length ? cells[tagColumn] : string.Empty).ToList();
    }

    private static bool IsTagHeader(string header) =>
        AssetBulkAssignmentFields.NormalizeHeader(header) is "assettag" or "tag" or "assettagserial" or "serialnumber" or "serial";
}
