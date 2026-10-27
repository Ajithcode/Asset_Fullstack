using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Bulk import of registered assets from an Excel sheet.</summary>
/// <remarks>
/// Each row is checked with the same rules as submitting the registration wizard, except that location levels
/// are given by name (the sheet cannot hold hierarchy ids). Valid rows are saved straight into AssetRegistration;
/// rows with errors are skipped and reported with their Excel row number.
/// </remarks>
public static class AssetImportEndpoints
{
    // Shared with AssetRegistrationEndpoints, so imported and wizard assets use one id series.
    private const string SequenceName = "asset-registration";
    private const string AssetRegistrationIdPrefix = "AR";
    private const string EntityType = "AssetRegistration";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd MMM yyyy", "d MMM yyyy", "MM/dd/yyyy"];

    public static RouteGroupBuilder MapAssetImportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations/import").WithTags("Asset Bulk Import");

        group.MapGet("/template", DownloadTemplate).WithName("DownloadAssetImportTemplate");
        group.MapPost("", ImportAssetsAsync).WithName("ImportAssets").DisableAntiforgery();

        return group;
    }

    /// <summary>The Excel template: the Assets sheet (with sample rows unless includeSampleData=false) and an Instructions sheet.</summary>
    private static IResult DownloadTemplate(bool includeSampleData = true)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(AssetImportColumns.SheetName);

        for (var index = 0; index < AssetImportColumns.All.Count; index++)
        {
            var column = AssetImportColumns.All[index];
            var cell = sheet.Cell(1, index + 1);
            cell.Value = column.Required ? $"{column.Header} *" : column.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = column.Required ? XLColor.FromHtml("#E9E5FF") : XLColor.FromHtml("#F2F2F2");
            if (column.Hint is not null)
            {
                cell.CreateComment().AddText(column.Hint);
            }
        }

        if (includeSampleData)
        {
            for (var row = 0; row < AssetImportSampleData.Rows.Count; row++)
            {
                var values = AssetImportSampleData.Rows[row];
                for (var index = 0; index < values.Length; index++)
                {
                    var cell = sheet.Cell(row + 2, index + 1);
                    switch (values[index])
                    {
                        case DateTime date:
                            cell.Value = date;
                            cell.Style.DateFormat.Format = "yyyy-mm-dd";
                            break;
                        case decimal number:
                            cell.Value = number;
                            break;
                        case int number:
                            cell.Value = number;
                            break;
                        case string text:
                            cell.Value = text;
                            break;
                    }
                }
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Max(2, AssetImportSampleData.Rows.Count + 1));

        var instructions = workbook.AddWorksheet("Instructions");
        instructions.Cell(1, 1).Value = "Column";
        instructions.Cell(1, 2).Value = "Required";
        instructions.Cell(1, 3).Value = "Notes";
        instructions.Row(1).Style.Font.Bold = true;
        for (var index = 0; index < AssetImportColumns.All.Count; index++)
        {
            var column = AssetImportColumns.All[index];
            instructions.Cell(index + 2, 1).Value = column.Header;
            instructions.Cell(index + 2, 2).Value = column.Required ? "Yes" : "No";
            instructions.Cell(index + 2, 3).Value = column.Hint ?? string.Empty;
        }

        var notesRow = AssetImportColumns.All.Count + 3;
        instructions.Cell(notesRow, 1).Value = $"Fill one asset per row on the '{AssetImportColumns.SheetName}' sheet, starting from row 2. Keep the header row.";
        instructions.Cell(notesRow + 1, 1).Value = $"At most {AssetImportColumns.MaxRows} assets per file. Replace or delete the sample rows before importing your own assets.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return Results.File(stream.ToArray(), ExcelContentType, "asset-import-template.xlsx");
    }

    /// <summary>
    /// Upload the filled template as form field "file". With validateOnly=true every row is checked and nothing is saved,
    /// so the screen can show errors before importing.
    /// </summary>
    private static async Task<IResult> ImportAssetsAsync(
        IFormFile? file,
        [FromQuery] bool? validateOnly,
        IAssetRegistrationRepository assetRegistrationRepository,
        IAssetRegistrationDraftRepository draftRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FileProblem("Upload the filled Excel template");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return FileProblem("Only .xlsx files can be imported");
        }

        if (file.Length > AssetImportColumns.MaxFileSizeBytes)
        {
            return FileProblem("File cannot be larger than 10 MB");
        }

        XLWorkbook workbook;
        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            return FileProblem("The file could not be read as an Excel workbook");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(worksheet => string.Equals(worksheet.Name, AssetImportColumns.SheetName, StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.First();

            var columnNumbers = ReadHeader(sheet);
            var missing = AssetImportColumns.All
                .Where(column => column.Required && !columnNumbers.ContainsKey(column.Header))
                .Select(column => column.Header)
                .ToList();
            if (missing.Count > 0)
            {
                return FileProblem($"These required columns are missing from the header row: {string.Join(", ", missing)}");
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var dataRows = Enumerable.Range(2, Math.Max(0, lastRow - 1))
                .Select(rowNumber => new SheetRow(sheet.Row(rowNumber), columnNumbers))
                .Where(row => !row.IsBlank)
                .ToList();

            if (dataRows.Count == 0)
            {
                return FileProblem("The sheet has no asset rows");
            }

            if (dataRows.Count > AssetImportColumns.MaxRows)
            {
                return FileProblem($"At most {AssetImportColumns.MaxRows} assets can be imported at once; the sheet has {dataRows.Count}");
            }

            var saveRows = validateOnly != true;
            var currentUser = currentUserAccessor.GetCurrentUser();
            var tagsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AssetImportRowResult>();

            foreach (var row in dataRows)
            {
                var (asset, errors) = ToAsset(row);
                var assetTag = asset.BasicInformation.AssetTag;

                if (assetTag is not null)
                {
                    if (!tagsInFile.Add(assetTag))
                    {
                        errors.Add(new AssetImportRowError(AssetImportColumns.AssetTag, $"Asset tag '{assetTag}' appears more than once in this file"));
                    }
                    else if (await assetRegistrationRepository.AssetTagExistsAsync(assetTag, cancellationToken))
                    {
                        errors.Add(new AssetImportRowError(AssetImportColumns.AssetTag, $"An asset with tag '{assetTag}' is already registered"));
                    }
                    else if (await draftRepository.AssetTagExistsAsync(assetTag, null, cancellationToken))
                    {
                        errors.Add(new AssetImportRowError(AssetImportColumns.AssetTag, $"Asset tag '{assetTag}' is already used by a draft registration"));
                    }
                }

                if (errors.Count > 0)
                {
                    results.Add(new AssetImportRowResult(row.RowNumber, asset.BasicInformation.AssetName, assetTag, AssetImportResults.Failed, null, errors));
                    continue;
                }

                if (!saveRows)
                {
                    results.Add(new AssetImportRowResult(row.RowNumber, asset.BasicInformation.AssetName, assetTag, AssetImportResults.Valid, null, []));
                    continue;
                }

                var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
                asset.AssetRegistrationId = $"{AssetRegistrationIdPrefix}{nextSequence:D6}";
                asset.CreatedBy = currentUser?.UserName;
                asset.CreatedAt = DateTime.UtcNow;
                asset.ClientId = currentUser?.ClientId;
                asset.TenantId = currentUser?.TenantId;
                asset.IsDeleted = false;

                await assetRegistrationRepository.CreateAsync(asset, cancellationToken);
                await eventLogger.LogAsync(EntityType, asset.AssetRegistrationId, asset.BasicInformation.AssetName ?? asset.AssetRegistrationId, EventAction.Created, cancellationToken);

                results.Add(new AssetImportRowResult(row.RowNumber, asset.BasicInformation.AssetName, assetTag, AssetImportResults.Imported, asset.AssetRegistrationId, []));
            }

            return Results.Ok(new AssetImportResponse(
                file.FileName,
                !saveRows,
                results.Count,
                results.Count(result => result.Result == AssetImportResults.Imported),
                results.Count(result => result.Result == AssetImportResults.Valid),
                results.Count(result => result.Result == AssetImportResults.Failed),
                results));
        }
    }

    /// <summary>Maps header text (without a trailing "*") to its column number.</summary>
    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet)
    {
        var columnNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim().TrimEnd('*').Trim();
            if (header.Length > 0)
            {
                columnNumbers.TryAdd(header, cell.Address.ColumnNumber);
            }
        }

        return columnNumbers;
    }

    /// <summary>Builds the asset from one row and checks it with the registration wizard's rules.</summary>
    private static (AssetRegistration Asset, List<AssetImportRowError> Errors) ToAsset(SheetRow row)
    {
        var basicInformation = new AssetBasicInformationDto(
            row.Text(AssetImportColumns.AssetName),
            row.Text(AssetImportColumns.AssetTag),
            row.Text(AssetImportColumns.Barcode),
            row.Text(AssetImportColumns.RfidTagId),
            row.Text(AssetImportColumns.Model),
            row.Text(AssetImportColumns.Manufacturer),
            row.Text(AssetImportColumns.SerialNumber),
            row.Text(AssetImportColumns.Status),
            row.Text(AssetImportColumns.Condition),
            row.Text(AssetImportColumns.Criticality),
            row.Text(AssetImportColumns.Description),
            null);

        var classification = new AssetClassificationDto(
            row.Text(AssetImportColumns.Category),
            row.Text(AssetImportColumns.SubCategory),
            row.Text(AssetImportColumns.AssetType),
            row.Text(AssetImportColumns.AssetSubType),
            row.Text(AssetImportColumns.FunctionPurpose),
            row.Text(AssetImportColumns.Criticality),
            row.Text(AssetImportColumns.Department),
            row.Text(AssetImportColumns.CostCenter),
            row.Text(AssetImportColumns.BusinessUnit),
            AssetRegistrationDraftValidationExtensions.NormalizeTaggingMethod(row.Text(AssetImportColumns.TaggingMethod)),
            row.Text(AssetImportColumns.ComplianceCategory),
            row.Text(AssetImportColumns.LifecycleStage),
            null);

        var locationAssignment = new AssetLocationAssignmentDto(
            new AssetLocationDto(
                null,
                null, row.Text(AssetImportColumns.Country),
                null, row.Text(AssetImportColumns.Site),
                null, row.Text(AssetImportColumns.Building),
                null, row.Text(AssetImportColumns.Floor),
                null, row.Text(AssetImportColumns.Zone),
                null, row.Text(AssetImportColumns.Room),
                null),
            new AssetAssignmentDto(
                row.Text(AssetImportColumns.CustodianDepartment),
                null,
                row.Text(AssetImportColumns.AssignedTo),
                row.Text(AssetImportColumns.OwnershipType),
                null,
                row.Text(AssetImportColumns.ResponsibleTeam),
                null,
                null,
                true));

        var warrantyPeriod = row.Decimal(AssetImportColumns.WarrantyPeriod);
        if (warrantyPeriod is not null && warrantyPeriod != Math.Floor(warrantyPeriod.Value))
        {
            row.Errors.Add(new AssetImportRowError(AssetImportColumns.WarrantyPeriod, "Warranty Period must be a whole number"));
        }

        var purchaseWarranty = new AssetPurchaseWarrantyDto(
            new AssetPurchaseDto(
                row.Text(AssetImportColumns.PurchaseOrderNumber),
                row.Text(AssetImportColumns.InvoiceNumber),
                row.Text(AssetImportColumns.SupplierVendor),
                row.Text(AssetImportColumns.Manufacturer),
                row.Date(AssetImportColumns.PurchaseDate),
                row.Date(AssetImportColumns.DeliveryDate),
                row.Decimal(AssetImportColumns.PurchaseCost),
                row.Decimal(AssetImportColumns.Quantity),
                null,
                row.Text(AssetImportColumns.Currency),
                row.Decimal(AssetImportColumns.TaxPercent),
                null,
                null,
                null,
                null,
                null),
            null,
            new AssetWarrantyDto(
                row.Text(AssetImportColumns.WarrantyType),
                row.Date(AssetImportColumns.WarrantyStartDate),
                null,
                warrantyPeriod is null ? null : (int)Math.Floor(warrantyPeriod.Value),
                row.Text(AssetImportColumns.WarrantyPeriodUnit),
                null,
                null,
                row.Text(AssetImportColumns.ServiceProvider),
                null));

        var errors = row.Errors;
        AddErrors(errors, basicInformation.Validate(saveAsDraft: false));
        AddErrors(errors, classification.Validate(saveAsDraft: false));
        AddErrors(errors, purchaseWarranty.Validate(saveAsDraft: false));

        // The wizard requires hierarchy ids, which a sheet cannot hold; here the level names are required instead.
        RequireText(errors, row, AssetImportColumns.Country);
        RequireText(errors, row, AssetImportColumns.Site);
        RequireText(errors, row, AssetImportColumns.Building);
        RequireText(errors, row, AssetImportColumns.Floor);
        RequireText(errors, row, AssetImportColumns.CustodianDepartment);

        var asset = new AssetRegistration
        {
            BasicInformation = basicInformation.ToSection(),
            Classification = classification.ToSection(),
            LocationAssignment = locationAssignment.ToSection(),
            PurchaseWarranty = purchaseWarranty.ToSection(),
            AdditionalInformation = new AssetAdditionalInformation()
        };

        return (asset, errors);
    }

    private static void AddErrors(List<AssetImportRowError> errors, Dictionary<string, string[]> validationErrors)
    {
        foreach (var (key, messages) in validationErrors)
        {
            var column = AssetImportColumns.HeaderForErrorKey(key);

            // A column that could not be read already has its own error (e.g. a bad date), so "is required" is not added on top.
            if (errors.Any(error => error.Column == column))
            {
                continue;
            }

            errors.AddRange(messages.Select(message => new AssetImportRowError(column, message)));
        }
    }

    private static void RequireText(List<AssetImportRowError> errors, SheetRow row, string column)
    {
        if (row.Text(column) is null)
        {
            errors.Add(new AssetImportRowError(column, $"{column} is required"));
        }
    }

    private static IResult FileProblem(string message)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["File"] = [message]
        });
    }

    /// <summary>Reads typed values from one sheet row; values that cannot be read are added to Errors.</summary>
    private sealed class SheetRow(IXLRow row, IReadOnlyDictionary<string, int> columnNumbers)
    {
        public int RowNumber { get; } = row.RowNumber();

        public List<AssetImportRowError> Errors { get; } = [];

        public bool IsBlank => columnNumbers.Values.All(columnNumber => row.Cell(columnNumber).Value.IsBlank
            || string.IsNullOrWhiteSpace(row.Cell(columnNumber).GetString()));

        public string? Text(string column)
        {
            var cell = Cell(column);
            if (cell is null || cell.Value.IsBlank)
            {
                return null;
            }

            // Dates typed into text columns come back as the date, not the serial number.
            var text = cell.Value.IsDateTime
                ? cell.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : cell.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        public DateTime? Date(string column)
        {
            var cell = Cell(column);
            if (cell is null || cell.Value.IsBlank)
            {
                return null;
            }

            if (cell.Value.IsDateTime)
            {
                return cell.Value.GetDateTime();
            }

            if (cell.Value.IsNumber)
            {
                return DateTime.FromOADate(cell.Value.GetNumber());
            }

            var text = cell.GetString().Trim();
            if (text.Length == 0)
            {
                return null;
            }

            if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            Errors.Add(new AssetImportRowError(column, $"'{text}' is not a valid date; use e.g. 2025-01-15"));
            return null;
        }

        public decimal? Decimal(string column)
        {
            var cell = Cell(column);
            if (cell is null || cell.Value.IsBlank)
            {
                return null;
            }

            if (cell.Value.IsNumber)
            {
                return (decimal)cell.Value.GetNumber();
            }

            var text = cell.GetString().Trim().Replace(",", string.Empty);
            if (text.Length == 0)
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            Errors.Add(new AssetImportRowError(column, $"'{text}' is not a valid number"));
            return null;
        }

        private IXLCell? Cell(string column) =>
            columnNumbers.TryGetValue(column, out var columnNumber) ? row.Cell(columnNumber) : null;
    }
}
