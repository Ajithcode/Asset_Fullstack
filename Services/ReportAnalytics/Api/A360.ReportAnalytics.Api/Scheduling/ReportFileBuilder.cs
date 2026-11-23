using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace A360.ReportAnalytics.Api.Scheduling;

/// <summary>A report as a title, a few "label: value" lines and one table.</summary>
public sealed record ReportTable(
    string Title,
    IReadOnlyList<(string Label, string Value)> Details,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string? Note = null);

public sealed record ReportFile(string FileName, string ContentType, byte[] Content);

/// <summary>Writes a <see cref="ReportTable"/> as Excel, CSV or PDF.</summary>
public static class ReportFileBuilder
{
    /// <summary>A PDF this long is already hard to read; the full data is in the Excel / CSV formats.</summary>
    public const int MaxPdfRows = 5000;

    public static ReportFile Build(ReportTable table, string format, string baseFileName) => format switch
    {
        ReportScheduleOptions.Csv => new ReportFile($"{baseFileName}.csv", "text/csv", Csv(table)),
        ReportScheduleOptions.Pdf => new ReportFile($"{baseFileName}.pdf", "application/pdf", Pdf(table)),
        _ => new ReportFile($"{baseFileName}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(table))
    };

    private static byte[] Csv(ReportTable table)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", table.Headers.Select(Field)));
        foreach (var row in table.Rows)
        {
            csv.AppendLine(string.Join(",", row.Select(Field)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        static string Field(string value) =>
            value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static byte[] Xlsx(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");

        sheet.Cell(1, 1).Value = table.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        var row = 2;
        foreach (var (label, value) in table.Details)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        if (table.Note is not null)
        {
            sheet.Cell(row++, 1).Value = table.Note;
        }

        row++;
        var headerRow = row;
        for (var column = 0; column < table.Headers.Count; column++)
        {
            sheet.Cell(headerRow, column + 1).Value = table.Headers[column];
        }

        sheet.Row(headerRow).Style.Font.Bold = true;
        sheet.Row(headerRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9E5FF");

        foreach (var values in table.Rows)
        {
            row++;
            for (var column = 0; column < values.Count; column++)
            {
                sheet.Cell(row, column + 1).Value = values[column];
            }
        }

        sheet.Columns().AdjustToContents(headerRow, Math.Min(row, headerRow + 500));
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] Pdf(ReportTable table)
    {
        var rows = table.Rows.Take(MaxPdfRows).ToList();
        var truncated = table.Rows.Count > rows.Count;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(table.Headers.Count > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(text => text.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Text(table.Title).FontSize(14).Bold();
                    foreach (var (label, value) in table.Details)
                    {
                        column.Item().Text(text =>
                        {
                            text.Span($"{label}: ").SemiBold();
                            text.Span(value);
                        });
                    }

                    if (table.Note is not null)
                    {
                        column.Item().Text(table.Note).Italic();
                    }

                    if (truncated)
                    {
                        column.Item().Text($"Showing the first {MaxPdfRows} of {table.Rows.Count} rows; choose Excel or CSV for all rows.").Italic();
                    }

                    column.Item().PaddingBottom(6);
                });

                page.Content().Table(grid =>
                {
                    grid.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in table.Headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    grid.Header(header =>
                    {
                        foreach (var title in table.Headers)
                        {
                            header.Cell().Background("#E9E5FF").Padding(3).Text(title).SemiBold();
                        }
                    });

                    foreach (var values in rows)
                    {
                        foreach (var value in values)
                        {
                            grid.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value);
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
