using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Endpoints;

/// <summary>A file read as a header row plus data rows of cell text.</summary>
/// <param name="Columns">Header text without a trailing "*"; blank headers become "Column N" and repeats get " (2)", " (3)"…</param>
/// <param name="Rows">Non-blank data rows with their row number in the file; cells are in Columns order.</param>
/// <param name="EmptyRowCount">Blank rows between data rows; they are skipped.</param>
internal sealed record SpreadsheetTable(
    List<string> Columns,
    List<(int RowNumber, List<string?> Cells)> Rows,
    int EmptyRowCount);

/// <summary>Reads and writes .xlsx and .csv files as plain tables.</summary>
internal static class SpreadsheetTableIO
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string CsvContentType = "text/csv";

    /// <summary>Reads the sheet called <paramref name="preferredSheet"/>, or the first sheet.</summary>
    public static SpreadsheetTable ReadWorkbook(Stream stream, string preferredSheet)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(worksheet => string.Equals(worksheet.Name, preferredSheet, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        var records = Enumerable.Range(1, lastRow)
            .Select(rowNumber => (rowNumber, Enumerable.Range(1, lastColumn).Select(column => CellText(sheet.Cell(rowNumber, column))).ToList()));

        return ToTable(records);
    }

    /// <summary>Reads a CSV file (comma, semicolon or tab separated; quoted values may hold separators and line breaks).</summary>
    public static SpreadsheetTable ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();

        var firstLine = text.Split('\n', 2)[0];
        var delimiter = new[] { ',', ';', '\t' }.OrderByDescending(candidate => firstLine.Count(character => character == candidate)).First();

        var records = new List<(int RowNumber, List<string?> Cells)>();
        var cells = new List<string?>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    line += character == '\n' ? 1 : 0;
                    cell.Append(character);
                }
            }
            else if (character == '"' && cell.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == delimiter)
            {
                cells.Add(ContractText.TrimOrNull(cell.ToString()));
                cell.Clear();
            }
            else if (character == '\n')
            {
                cells.Add(ContractText.TrimOrNull(cell.ToString()));
                records.Add((recordLine, cells));
                cells = [];
                cell.Clear();
                line++;
                recordLine = line;
            }
            else if (character != '\r')
            {
                cell.Append(character);
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(ContractText.TrimOrNull(cell.ToString()));
            records.Add((recordLine, cells));
        }

        return ToTable(records);
    }

    /// <summary>A CSV file (UTF-8 with BOM so Excel shows non-ASCII text correctly).</summary>
    public static byte[] WriteCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(CsvValue)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(value => CsvValue(FormatValue(value)))));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    /// <summary>Adds a sheet with a bold, frozen header row and typed cells (dates as dates, numbers as numbers).</summary>
    public static IXLWorksheet AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sheet = workbook.AddWorksheet(name);
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                SetCell(sheet.Cell(rowNumber, index + 1), row[index]);
            }

            rowNumber++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, Math.Max(1, headers.Count)).AdjustToContents(1, Math.Min(rowNumber, 200));
        return sheet;
    }

    public static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = date.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
                break;
            case decimal number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            case double number:
                cell.Value = number;
                break;
            case bool flag:
                cell.Value = flag ? "Yes" : "No";
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    public static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        bool flag => flag ? "Yes" : "No",
        _ => value.ToString()
    };

    private static string CsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // A leading = + - @ would be run as a formula by Excel.
        if (value[0] is '=' or '+' or '@' || (value[0] == '-' && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static SpreadsheetTable ToTable(IEnumerable<(int RowNumber, List<string?> Cells)> source)
    {
        var records = source.ToList();

        // Data in columns past the last header still counts; those columns get a "Column N" header.
        var width = records.Count == 0 ? 0 : records.Max(record => record.Cells.FindLastIndex(cell => cell is not null) + 1);
        var columns = new List<string>();
        var rows = new List<(int RowNumber, List<string?> Cells)>();
        var emptyRows = 0;
        var pendingEmptyRows = 0;
        var isHeader = true;

        foreach (var (rowNumber, cells) in records)
        {
            if (isHeader)
            {
                isHeader = false;
                for (var index = 0; index < width; index++)
                {
                    var header = index < cells.Count ? cells[index]?.TrimEnd('*').Trim() : null;
                    header = string.IsNullOrEmpty(header) ? $"Column {index + 1}" : header;

                    var unique = header;
                    for (var copy = 2; columns.Contains(unique, StringComparer.OrdinalIgnoreCase); copy++)
                    {
                        unique = $"{header} ({copy})";
                    }

                    columns.Add(unique);
                }

                continue;
            }

            var values = Enumerable.Range(0, columns.Count).Select(index => index < cells.Count ? cells[index] : null).ToList();
            if (values.All(value => value is null))
            {
                pendingEmptyRows++;
                continue;
            }

            if (rows.Count > 0)
            {
                emptyRows += pendingEmptyRows;
            }

            pendingEmptyRows = 0;
            rows.Add((rowNumber, values));
        }

        return new SpreadsheetTable(columns, rows, emptyRows);
    }

    private static string? CellText(IXLCell cell)
    {
        if (cell.Value.IsBlank)
        {
            return null;
        }

        // Dates come back as the date, not the serial number; numbers without the culture's separators.
        var text = cell.Value.IsDateTime
            ? cell.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : cell.Value.IsNumber
                ? cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture)
                : cell.GetString();
        return ContractText.TrimOrNull(text);
    }
}
