using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// The Bulk Import / Export screen (Assets › Bulk Import/Export).
/// </summary>
/// <remarks>
/// Import Assets runs in five steps:
/// 1. Download Template: GET /template.
/// 2. Upload File: POST /imports stores the file's rows (Excel or CSV) and auto maps its columns.
/// 3. Map Fields: GET/PUT /imports/{id}/mapping; Auto Map is POST /imports/{id}/mapping/auto.
/// 4. Validate: POST /imports/{id}/validate checks every row; GET /imports/{id}/records lists them by status
///    (Validation Results › View Details, Import Preview) and GET /imports/{id}/errors/export downloads the problem rows.
/// 5. Import: POST /imports/{id}/confirm creates or updates the valid rows' assets.
/// Export Assets is POST /exports (the file) and GET /exports/count (the number on the Export button).
/// Both appear in Import History: GET /history.
/// </remarks>
public static class AssetImportExportEndpoints
{
    private const string EntityType = "AssetImportExport";
    private const string AssetEntityType = "AssetRegistration";
    private const string ImportSequenceName = "asset-import";
    private const string ExportSequenceName = "asset-export";
    private const string ImportNumberPrefix = "IMP-";
    private const string ExportNumberPrefix = "EXP-";

    // Shared with AssetRegistrationEndpoints, so imported assets continue the same AR numbers.
    private const string AssetSequenceName = "asset-registration";
    private const string AssetRegistrationIdPrefix = "AR";

    private const int PreviewRowCount = 10;
    private const int ProblemPreviewCount = 50;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 200;

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy", "dd MMM yyyy", "d MMM yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "MM/dd/yyyy",
         "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"];

    public static RouteGroupBuilder MapAssetImportExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-import-export").WithTags("Asset Bulk Import / Export");

        group.MapGet("/options", () => Results.Ok(AssetImportExportOptionsResponse.Instance)).WithName("GetAssetImportExportOptions");
        group.MapGet("/fields", GetFields).WithName("GetAssetImportExportFields");

        // Step 1
        group.MapGet("/template", DownloadTemplate).WithName("DownloadAssetImportExportTemplate");

        // Step 2
        group.MapPost("/imports", UploadAsync).WithName("UploadAssetImport").DisableAntiforgery();
        group.MapGet("/imports/{id}", GetImportAsync).WithName("GetAssetImport");
        group.MapDelete("/imports/{id}", DeleteImportAsync).WithName("DeleteAssetImport");

        // Step 3
        group.MapGet("/imports/{id}/mapping", GetMappingAsync).WithName("GetAssetImportMapping");
        group.MapPut("/imports/{id}/mapping", SaveMappingAsync).WithName("SaveAssetImportMapping");
        group.MapPost("/imports/{id}/mapping/auto", AutoMapAsync).WithName("AutoMapAssetImport");

        // Step 4
        group.MapPost("/imports/{id}/validate", ValidateAsync).WithName("ValidateAssetImport");
        group.MapGet("/imports/{id}/records", GetRecordsAsync).WithName("GetAssetImportRecords");
        group.MapGet("/imports/{id}/errors/export", ExportProblemsAsync).WithName("ExportAssetImportErrors");

        // Step 5
        group.MapPost("/imports/{id}/confirm", ConfirmAsync).WithName("ConfirmAssetImport");

        // Export Assets
        group.MapGet("/exports/count", CountExportAsync).WithName("CountAssetExport");
        group.MapPost("/exports", ExportAsync).WithName("ExportAssets");

        // Import History
        group.MapGet("/history", GetHistoryAsync).WithName("GetAssetImportExportHistory");

        return group;
    }

    /// <summary>The Asset360 fields a column can be mapped to (and that can be exported), with Mandatory set for the import type.</summary>
    private static IResult GetFields(string? importType = null, bool importableOnly = false)
    {
        var type = AssetImportTypes.Normalize(importType) ?? AssetImportTypes.Create;
        return Results.Ok(AssetImportExportFields.All
            .Where(field => !importableOnly || field.Importable)
            .Select(field => AssetImportFieldResponse.FromField(field, type)));
    }

    /// <summary>Step 1: the template picked in Select Template, as .xlsx (with an Instructions sheet) or .csv.</summary>
    private static IResult DownloadTemplate(string? template = null, bool includeSampleData = true, string? fileFormat = null)
    {
        var picked = AssetImportTemplates.Find(template ?? AssetImportTemplates.Basic);
        if (picked is null)
        {
            return Problem("Template", $"Template must be one of: {string.Join(", ", AssetImportTemplates.All.Select(item => item.Key))}");
        }

        var format = AssetExportOptions.FileFormats.Normalize(fileFormat ?? AssetExportOptions.Xlsx);
        if (format is null)
        {
            return Problem("FileFormat", "FileFormat must be xlsx or csv");
        }

        var fields = AssetImportTemplates.Fields(picked.Key);
        var headers = fields.Select(field => field.IsMandatory(picked.ImportType) ? $"{field.Label} *" : field.Label).ToList();

        // The update template has no sample rows: they would point at assets that do not exist.
        var sampleRows = includeSampleData && picked.ImportType != AssetImportTypes.Update
            ? SampleRows(fields)
            : [];

        var fileName = $"asset-import-template-{picked.Key}";
        if (format == AssetExportOptions.Csv)
        {
            return Results.File(SpreadsheetTableIO.WriteCsv(headers, sampleRows), SpreadsheetTableIO.CsvContentType, $"{fileName}.csv");
        }

        using var workbook = new XLWorkbook();
        var sheet = SpreadsheetTableIO.AddSheet(workbook, AssetImportExportFields.SheetName, headers, sampleRows);
        for (var index = 0; index < fields.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            var mandatory = fields[index].IsMandatory(picked.ImportType);
            cell.Style.Fill.BackgroundColor = mandatory ? XLColor.FromHtml("#E9E5FF") : XLColor.FromHtml("#F2F2F2");
            if (fields[index].Hint is { } hint)
            {
                cell.CreateComment().AddText(hint);
            }
        }

        var instructions = workbook.AddWorksheet("Instructions");
        instructions.Cell(1, 1).Value = "Column";
        instructions.Cell(1, 2).Value = "Section";
        instructions.Cell(1, 3).Value = "Data Type";
        instructions.Cell(1, 4).Value = "Mandatory";
        instructions.Cell(1, 5).Value = "Notes";
        instructions.Row(1).Style.Font.Bold = true;
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            instructions.Cell(index + 2, 1).Value = field.Label;
            instructions.Cell(index + 2, 2).Value = field.Group;
            instructions.Cell(index + 2, 3).Value = field.DataType;
            instructions.Cell(index + 2, 4).Value = field.IsMandatory(picked.ImportType) ? "Yes" : "No";
            instructions.Cell(index + 2, 5).Value = field.Hint ?? string.Empty;
        }

        var notesRow = fields.Count + 3;
        instructions.Cell(notesRow, 1).Value = $"Fill one asset per row on the '{AssetImportExportFields.SheetName}' sheet, starting from row 2. Keep the header row.";
        instructions.Cell(notesRow + 1, 1).Value = $"At most {AssetImportExportFields.MaxRows} rows per file. Replace or delete the sample rows before importing your own assets.";
        instructions.Cell(notesRow + 2, 1).Value = "When updating, assets are found by Asset ID (or by Asset Tag when Asset ID is blank). Blank cells keep the current value.";
        instructions.Columns().AdjustToContents();

        return Results.File(SpreadsheetTableIO.Save(workbook), SpreadsheetTableIO.ExcelContentType, $"{fileName}.xlsx");
    }

    /// <summary>
    /// Step 2: upload the file as form field "file" (.xlsx or .csv, at most 10 MB, header in row 1), with the
    /// Import Type ("Create", "Update" or "Upsert"; default Create) and the template key picked on step 1.
    /// Columns are mapped automatically; the mapping can be changed on step 3.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        [FromForm] string? importType,
        [FromForm] string? template,
        IAssetImportExportJobRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var type = AssetImportTypes.Normalize(importType ?? AssetImportTypes.Create);
        if (type is null)
        {
            return Problem("ImportType", $"ImportType must be one of: {string.Join(", ", AssetImportTypes.All)}");
        }

        if (file is null || file.Length == 0)
        {
            return Problem("File", "Upload an Excel or CSV file");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return Problem("File", extension == ".xls"
                ? "Old .xls files cannot be read; save the file as .xlsx and upload it again"
                : "Only .xlsx and .csv files can be uploaded");
        }

        if (file.Length > AssetImportExportFields.MaxFileSizeBytes)
        {
            return Problem("File", "File cannot be larger than 10 MB");
        }

        SpreadsheetTable table;
        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;
            table = extension == ".csv" ? SpreadsheetTableIO.ReadCsv(stream) : SpreadsheetTableIO.ReadWorkbook(stream, AssetImportExportFields.SheetName);
        }
        catch (Exception)
        {
            return Problem("File", extension == ".csv" ? "The file could not be read as CSV" : "The file could not be read as an Excel workbook");
        }

        if (table.Columns.Count == 0)
        {
            return Problem("File", "The first row must be the header row with the column names");
        }

        if (table.Columns.Count > AssetImportExportFields.MaxColumns)
        {
            return Problem("File", $"The file cannot have more than {AssetImportExportFields.MaxColumns} columns");
        }

        if (table.Rows.Count == 0)
        {
            return Problem("File", "The file has no data rows");
        }

        if (table.Rows.Count > AssetImportExportFields.MaxRows)
        {
            return Problem("File", $"At most {AssetImportExportFields.MaxRows} assets can be imported at once; the file has {table.Rows.Count}");
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(ImportSequenceName, cancellationToken);
        var currentUser = currentUserAccessor.GetCurrentUser();
        var suggested = SuggestMapping(table.Columns, type);

        var job = new AssetImportExportJob
        {
            JobNumber = $"{ImportNumberPrefix}{nextSequence:D6}",
            Type = AssetImportExportJobRepository.ImportType,
            FileName = Path.GetFileName(file.FileName),
            FileSize = file.Length,
            FileFormat = extension.TrimStart('.'),
            RecordCount = table.Rows.Count,
            Import = new AssetImportJobDetails
            {
                ImportType = type,
                Template = AssetImportTemplates.Find(template)?.Key,
                CurrentStep = 2,
                Columns = table.Columns,
                EmptyRowCount = table.EmptyRowCount,
                Mapping = suggested.Select(pair => new AssetImportColumnMap { Column = pair.Key, Field = pair.Value }).ToList(),
                Rows = table.Rows.Select(row => new AssetImportJobRow { RowNumber = row.RowNumber, Cells = row.Cells }).ToList()
            },
            Status = AssetImportExportJobRepository.UploadedStatus,
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };

        await repository.CreateAsync(job, cancellationToken);
        await eventLogger.LogAsync(EntityType, job.Id, $"{job.JobNumber} - {job.FileName} uploaded", EventAction.Created, cancellationToken);

        var mapping = BuildMappingResponse(job);
        var fileChecks = new List<AssetImportFileCheck>
        {
            new("File format is valid", true, null),
            new("Mandatory columns found", mapping.MissingMandatory.Count == 0,
                mapping.MissingMandatory.Count == 0 ? null : $"Not found by name: {string.Join(", ", mapping.MissingMandatory)}. Map them in the next step."),
            new("No empty rows", job.Import.EmptyRowCount == 0,
                job.Import.EmptyRowCount == 0 ? null : $"{job.Import.EmptyRowCount} empty rows were skipped"),
            new("Data read successfully", true, $"{job.RecordCount} records, {job.Import.Columns.Count} columns")
        };

        return Results.Created(
            $"/api/asset-import-export/imports/{job.Id}",
            new AssetImportUploadResponse(AssetImportJobResponse.FromEntity(job), fileChecks, mapping));
    }

    private static async Task<IResult> GetImportAsync(string id, IAssetImportExportJobRepository repository, CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        return job is null ? ImportNotFound(id) : Results.Ok(AssetImportJobResponse.FromEntity(job));
    }

    /// <summary>Cancel / remove the file (×): deletes an import that has not been imported. Imported ones stay in the history.</summary>
    private static async Task<IResult> DeleteImportAsync(
        string id,
        IAssetImportExportJobRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        if (IsFinished(job))
        {
            return AlreadyImported(job);
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return ImportNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, job.Id, $"{job.JobNumber} - {job.FileName} cancelled", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Step 3: the Field Mapping table, one row per file column.</summary>
    private static async Task<IResult> GetMappingAsync(string id, IAssetImportExportJobRepository repository, CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        return job is null ? ImportNotFound(id) : Results.Ok(BuildMappingResponse(job));
    }

    /// <summary>
    /// Step 3: saves the mapping (file column → field key; null or "" leaves a column unmapped) and, when given, the import type.
    /// Saving clears any earlier validation, so Validate has to run again.
    /// </summary>
    private static async Task<IResult> SaveMappingAsync(
        string id,
        SaveAssetImportMappingRequest request,
        IAssetImportExportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        if (IsFinished(job))
        {
            return AlreadyImported(job);
        }

        var import = job.Import!;
        var errors = new Dictionary<string, string[]>();

        var importType = import.ImportType;
        if (request.ImportType is not null)
        {
            var normalized = AssetImportTypes.Normalize(request.ImportType);
            if (normalized is null)
            {
                errors["ImportType"] = [$"ImportType must be one of: {string.Join(", ", AssetImportTypes.All)}"];
            }
            else
            {
                importType = normalized;
            }
        }

        var mapping = new List<AssetImportColumnMap>();
        var mappedFrom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (column, key) in request.Mapping ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var fileColumn = import.Columns.FirstOrDefault(name => string.Equals(name, column?.Trim(), StringComparison.OrdinalIgnoreCase));
            var field = AssetImportExportFields.Find(key);
            if (fileColumn is null)
            {
                errors[$"Mapping.{column}"] = [$"Column '{column}' is not in the file"];
            }
            else if (field is null)
            {
                errors[$"Mapping.{column}"] = [$"'{key}' is not an Asset360 field"];
            }
            else if (!field.Importable)
            {
                errors[$"Mapping.{column}"] = [$"{field.Label} is calculated by Asset360 and cannot be imported"];
            }
            else if (mappedFrom.TryGetValue(field.Key, out var otherColumn))
            {
                errors[$"Mapping.{column}"] = [$"{field.Label} is already mapped from column '{otherColumn}'"];
            }
            else
            {
                mappedFrom[field.Key] = fileColumn;
                mapping.Add(new AssetImportColumnMap { Column = fileColumn, Field = field.Key });
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        import.ImportType = importType;
        import.Mapping = mapping;
        ResetValidation(job, AssetImportExportJobRepository.MappedStatus, step: 3);

        if (!await SaveAsync(job, repository, currentUserAccessor, cancellationToken))
        {
            return ImportNotFound(id);
        }

        return Results.Ok(BuildMappingResponse(job));
    }

    /// <summary>Auto Map: maps every column whose header matches a field name (or a common alternative name), replacing the current mapping.</summary>
    private static async Task<IResult> AutoMapAsync(
        string id,
        IAssetImportExportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        if (IsFinished(job))
        {
            return AlreadyImported(job);
        }

        var import = job.Import!;
        import.Mapping = SuggestMapping(import.Columns, import.ImportType)
            .Select(pair => new AssetImportColumnMap { Column = pair.Key, Field = pair.Value })
            .ToList();
        ResetValidation(job, AssetImportExportJobRepository.MappedStatus, step: 3);

        if (!await SaveAsync(job, repository, currentUserAccessor, cancellationToken))
        {
            return ImportNotFound(id);
        }

        return Results.Ok(BuildMappingResponse(job));
    }

    /// <summary>
    /// Step 4: checks every row with the registration wizard's rules and sorts it into Valid, Invalid or Duplicate.
    /// Nothing is saved to the assets yet.
    /// </summary>
    private static async Task<IResult> ValidateAsync(
        string id,
        IAssetImportExportJobRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetRegistrationDraftRepository draftRepository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        if (IsFinished(job))
        {
            return AlreadyImported(job);
        }

        if (MissingMandatory(job.Import!) is { Count: > 0 } missing)
        {
            return Problem("Mapping", $"Map the mandatory fields: {string.Join(", ", missing)}");
        }

        await CheckRowsAsync(job, assetRepository, draftRepository, cancellationToken);
        job.Status = AssetImportExportJobRepository.ValidatedStatus;
        job.Import!.CurrentStep = 4;

        if (!await SaveAsync(job, repository, currentUserAccessor, cancellationToken))
        {
            return ImportNotFound(id);
        }

        var import = job.Import;
        return Results.Ok(new AssetImportValidationResponse(
            AssetImportJobResponse.FromEntity(job),
            AssetImportJobSummaryResponse.FromEntity(import.Summary)!,
            import.Rows.Where(row => row.Status == AssetImportRowStatuses.Valid).Take(PreviewRowCount).Select(row => ToRecord(import, row)).ToList(),
            import.Rows.Where(row => row.Status is AssetImportRowStatuses.Invalid or AssetImportRowStatuses.Duplicate)
                .Take(ProblemPreviewCount).Select(row => ToRecord(import, row)).ToList()));
    }

    /// <summary>View Details / Import Preview: the rows with their mapped values and issues; status = Valid, Invalid, Duplicate, Imported or Failed.</summary>
    private static async Task<IResult> GetRecordsAsync(
        string id,
        IAssetImportExportJobRepository repository,
        CancellationToken cancellationToken,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        IEnumerable<AssetImportJobRow> rows = job.Import!.Rows;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = AssetImportRowStatuses.Normalize(status);
            if (wanted is null)
            {
                return Problem("Status", "Status must be Valid, Invalid, Duplicate, Imported or Failed");
            }

            rows = rows.Where(row => row.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            rows = rows.Where(row => row.Cells.Any(cell => cell?.Contains(text, StringComparison.OrdinalIgnoreCase) == true));
        }

        var matching = rows.ToList();
        (page, pageSize) = Paging(page, pageSize);
        return Results.Ok(new AssetImportRecordsResponse(
            matching.Skip((page - 1) * pageSize).Take(pageSize).Select(row => ToRecord(job.Import, row)).ToList(),
            matching.Count,
            page,
            pageSize));
    }

    /// <summary>
    /// The invalid and duplicate rows as an Excel file: the file's own columns plus Row Number, Status and Errors,
    /// so the rows can be fixed and uploaded again.
    /// </summary>
    private static async Task<IResult> ExportProblemsAsync(string id, IAssetImportExportJobRepository repository, CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        var import = job.Import!;
        if (import.Summary is null)
        {
            return Problem("Import", "Validate the file first");
        }

        var headers = import.Columns.Concat(["Row Number", "Status", "Errors"]).ToList();
        var rows = import.Rows
            .Where(row => row.Status is AssetImportRowStatuses.Invalid or AssetImportRowStatuses.Duplicate or AssetImportRowStatuses.Failed)
            .Select(row => (IReadOnlyList<object?>)row.Cells.Cast<object?>()
                .Concat([row.RowNumber, row.Status, string.Join("; ", row.Issues.Select(IssueText))])
                .ToList());

        using var workbook = new XLWorkbook();
        SpreadsheetTableIO.AddSheet(workbook, AssetImportExportFields.SheetName, headers, rows);
        var fileName = $"{Path.GetFileNameWithoutExtension(job.FileName)}-errors.xlsx";
        return Results.File(SpreadsheetTableIO.Save(workbook), SpreadsheetTableIO.ExcelContentType, fileName);
    }

    /// <summary>
    /// Step 5 Import: checks the rows again (assets may have changed since Validate) and saves every valid one,
    /// creating new assets or updating existing ones. Invalid and duplicate rows are skipped and reported as Failed.
    /// </summary>
    private static async Task<IResult> ConfirmAsync(
        string id,
        IAssetImportExportJobRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetRegistrationDraftRepository draftRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var job = await FindImportAsync(id, repository, cancellationToken);
        if (job is null)
        {
            return ImportNotFound(id);
        }

        if (IsFinished(job))
        {
            return AlreadyImported(job);
        }

        var import = job.Import!;
        if (import.Summary is null)
        {
            return Problem("Import", "Validate the file before importing");
        }

        if (MissingMandatory(import) is { Count: > 0 } missing)
        {
            return Problem("Mapping", $"Map the mandatory fields: {string.Join(", ", missing)}");
        }

        var checkedRows = await CheckRowsAsync(job, assetRepository, draftRepository, cancellationToken);
        if (import.Summary.Valid == 0)
        {
            await SaveAsync(job, repository, currentUserAccessor, cancellationToken);
            return Problem("Import", "No row is valid; fix the errors and upload the file again");
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        foreach (var (row, asset) in checkedRows)
        {
            if (asset is null)
            {
                continue;
            }

            try
            {
                if (row.Action == AssetImportRowActions.Create)
                {
                    var nextSequence = await sequenceGenerator.GetNextValueAsync(AssetSequenceName, cancellationToken);
                    asset.AssetRegistrationId = $"{AssetRegistrationIdPrefix}{nextSequence:D6}";
                    asset.CreatedBy = currentUser?.UserName;
                    asset.CreatedAt = now;
                    asset.ClientId = currentUser?.ClientId;
                    asset.TenantId = currentUser?.TenantId;
                    asset.IsDeleted = false;

                    await assetRepository.CreateAsync(asset, cancellationToken);
                    await eventLogger.LogAsync(AssetEntityType, asset.AssetRegistrationId, asset.BasicInformation.AssetName ?? asset.AssetRegistrationId,
                        EventAction.Created, cancellationToken);
                    created++;
                }
                else
                {
                    asset.UpdatedBy = currentUser?.UserName;
                    asset.UpdatedAt = now;
                    if (!await assetRepository.UpdateAsync(asset.Id, asset, cancellationToken))
                    {
                        MarkFailed(row, "The asset was deleted before it could be updated");
                        continue;
                    }

                    await eventLogger.LogAsync(AssetEntityType, asset.AssetRegistrationId, asset.BasicInformation.AssetName ?? asset.AssetRegistrationId,
                        EventAction.Updated, cancellationToken);
                    updated++;
                }

                row.Status = AssetImportRowStatuses.Imported;
                row.AssetRegistrationId = asset.AssetRegistrationId;
            }
            catch (MongoException)
            {
                MarkFailed(row, "The asset could not be saved; try importing this row again");
            }
        }

        // Rows that did not pass the checks were not imported.
        foreach (var row in import.Rows.Where(row => row.Status is AssetImportRowStatuses.Invalid or AssetImportRowStatuses.Duplicate))
        {
            row.Status = AssetImportRowStatuses.Failed;
        }

        var summary = import.Summary;
        summary.Created = created;
        summary.Updated = updated;
        summary.Failed = import.Rows.Count(row => row.Status == AssetImportRowStatuses.Failed);

        import.CurrentStep = 5;
        job.Status = created + updated > 0 ? AssetImportExportJobRepository.CompletedStatus : AssetImportExportJobRepository.FailedStatus;
        job.CompletedAt = now;
        job.CompletedBy = currentUser?.UserName;
        await SaveAsync(job, repository, currentUserAccessor, cancellationToken);

        await eventLogger.LogAsync(EntityType, job.Id,
            $"{job.JobNumber} - {job.FileName} imported: {created} created, {updated} updated, {summary.Failed} failed",
            EventAction.Updated, cancellationToken);

        return Results.Ok(new AssetImportConfirmResponse(
            AssetImportJobResponse.FromEntity(job),
            AssetImportJobSummaryResponse.FromEntity(summary)!,
            import.Rows.Where(row => row.Status == AssetImportRowStatuses.Failed).Take(ProblemPreviewCount).Select(row => ToRecord(import, row)).ToList()));
    }

    /// <summary>The number of assets the current export filters match (the count on the Export button).</summary>
    private static async Task<IResult> CountExportAsync(
        [AsParameters] AssetExportFilters filters,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var count = await assetRepository.CountForExportAsync(ToQuery(filters), cancellationToken);
        return Results.Ok(new AssetExportCountResponse(count, AssetExportOptions.MaxRows));
    }

    /// <summary>
    /// Export Assets: the matching assets as .xlsx or .csv, one row per asset with Asset ID first, so the file can be
    /// edited and imported back with Update Existing Assets. Asset with Related Data adds Components, Assignments,
    /// Specifications and Custom Fields sheets.
    /// </summary>
    private static async Task<IResult> ExportAsync(
        AssetExportRequest request,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssetImportExportJobRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var exportType = AssetExportOptions.ExportTypes.Normalize(request.ExportType ?? AssetExportOptions.AssetMasterData);
        if (exportType is null)
        {
            errors["ExportType"] = [$"ExportType must be one of: {string.Join(", ", AssetExportOptions.ExportTypes.All)}"];
        }

        var format = AssetExportOptions.FileFormats.Normalize(request.FileFormat ?? AssetExportOptions.Xlsx);
        if (format is null)
        {
            errors["FileFormat"] = ["FileFormat must be xlsx or csv"];
        }
        else if (exportType == AssetExportOptions.AssetWithRelatedData && format == AssetExportOptions.Csv)
        {
            errors["FileFormat"] = ["Asset with Related Data has several sheets, so it can only be exported as Excel (.xlsx)"];
        }

        var fieldSet = AssetExportOptions.FieldSets.Normalize(request.FieldSet ?? AssetExportOptions.StandardFields);
        List<AssetImportField> fields = [];
        if (request.Fields is { Count: > 0 })
        {
            fieldSet = AssetExportOptions.CustomFields;
            foreach (var key in request.Fields.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (AssetImportExportFields.Find(key) is { } field)
                {
                    fields.Add(field);
                }
                else
                {
                    errors["Fields"] = [$"'{key}' is not an Asset360 field"];
                }
            }
        }
        else if (exportType == AssetExportOptions.CustomExport || fieldSet == AssetExportOptions.CustomFields)
        {
            errors["Fields"] = ["Pick the fields to export"];
        }
        else if (fieldSet is null)
        {
            errors["FieldSet"] = ["FieldSet must be Standard or All"];
        }
        else
        {
            fields = fieldSet == AssetExportOptions.AllFields
                ? AssetImportExportFields.All.ToList()
                : AssetImportExportFields.All.Where(field => field.Standard).ToList();
        }

        var filters = request.Filters ?? new AssetExportFilters(null, null, null, null, null, null, null, null, null, null, null);
        if (filters.NextMaintenanceFrom is { } from && filters.NextMaintenanceTo is { } to && to.Date < from.Date)
        {
            errors["Filters.NextMaintenanceTo"] = ["The end of the date range cannot be before its start"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = ToQuery(filters);
        var count = await assetRepository.CountForExportAsync(query, cancellationToken);
        if (count > AssetExportOptions.MaxRows)
        {
            return Problem("Filters", $"{count} assets match; at most {AssetExportOptions.MaxRows} can be exported at once. Narrow the filters and export in parts.");
        }

        var assets = await assetRepository.FindForExportAsync(query, AssetExportOptions.MaxRows, cancellationToken);
        var headers = fields.Select(field => field.Label).ToList();
        var rows = assets.Select(asset => (IReadOnlyList<object?>)fields.Select(field => field.Get(asset)).ToList()).ToList();

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        string fileName;
        byte[] content;
        string contentType;

        if (format == AssetExportOptions.Csv)
        {
            fileName = $"assets-export-{stamp}.csv";
            content = SpreadsheetTableIO.WriteCsv(headers, rows);
            contentType = SpreadsheetTableIO.CsvContentType;
        }
        else
        {
            using var workbook = new XLWorkbook();
            SpreadsheetTableIO.AddSheet(workbook, AssetImportExportFields.SheetName, headers, rows);
            if (exportType == AssetExportOptions.AssetWithRelatedData)
            {
                await AddRelatedSheetsAsync(workbook, assets, componentRepository, assignmentRepository, cancellationToken);
            }

            fileName = $"assets-export-{stamp}.xlsx";
            content = SpreadsheetTableIO.Save(workbook);
            contentType = SpreadsheetTableIO.ExcelContentType;
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(ExportSequenceName, cancellationToken);
        var currentUser = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        var job = new AssetImportExportJob
        {
            JobNumber = $"{ExportNumberPrefix}{nextSequence:D6}",
            Type = AssetImportExportJobRepository.ExportType,
            FileName = fileName,
            FileSize = content.LongLength,
            FileFormat = format!,
            RecordCount = assets.Count,
            CompletedAt = now,
            CompletedBy = currentUser?.UserName,
            Export = new AssetExportJobDetails
            {
                ExportType = exportType!,
                FieldSet = fieldSet!,
                Fields = fields.Select(field => field.Key).ToList(),
                Filters = new AssetExportJobFilters
                {
                    Search = ContractText.TrimOrNull(filters.Search),
                    Category = ContractText.TrimOrNull(filters.Category),
                    AssetType = ContractText.TrimOrNull(filters.AssetType),
                    Status = ContractText.TrimOrNull(filters.Status),
                    LocationId = ContractText.TrimOrNull(filters.LocationId),
                    Department = ContractText.TrimOrNull(filters.Department),
                    Criticality = ContractText.TrimOrNull(filters.Criticality),
                    Condition = ContractText.TrimOrNull(filters.Condition),
                    Manufacturer = ContractText.TrimOrNull(filters.Manufacturer),
                    NextMaintenanceFrom = filters.NextMaintenanceFrom?.Date,
                    NextMaintenanceTo = filters.NextMaintenanceTo?.Date
                }
            },
            Status = AssetImportExportJobRepository.CompletedStatus,
            CreatedBy = currentUser?.UserName,
            CreatedAt = now,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };

        await repository.CreateAsync(job, cancellationToken);
        await eventLogger.LogAsync(EntityType, job.Id, $"{job.JobNumber} - {assets.Count} assets exported", EventAction.Created, cancellationToken);

        return Results.File(content, contentType, fileName);
    }

    /// <summary>
    /// Import History, newest first. By default only finished imports and exports are listed;
    /// includeInProgress=true adds imports that were uploaded but not imported yet.
    /// </summary>
    private static async Task<IResult> GetHistoryAsync(
        IAssetImportExportJobRepository repository,
        CancellationToken cancellationToken,
        string? type = null,
        string? status = null,
        string? search = null,
        bool includeInProgress = false,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        string? normalizedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            normalizedType = new OptionList(AssetImportExportJobRepository.ImportType, AssetImportExportJobRepository.ExportType).Normalize(type);
            if (normalizedType is null)
            {
                return Problem("Type", "Type must be Import or Export");
            }
        }

        (page, pageSize) = Paging(page, pageSize);
        var result = await repository.QueryHistoryAsync(
            new AssetImportExportHistoryQuery(normalizedType, status, search, includeInProgress, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetImportExportHistoryResponse(
            result.Items.Select(AssetImportExportHistoryItemResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    /// <summary>
    /// Checks every row and records its status, action and issues on the job, together with the summary counts.
    /// Returns each row with the asset to save, or null when the row cannot be imported.
    /// </summary>
    /// <remarks>
    /// A row is Duplicate when its asset tag is already registered (Create), used by a draft registration, repeated in the file,
    /// or when two rows update the same asset; Invalid when a value cannot be read, a mandatory value is missing or the asset to
    /// update is not found. Blank cells keep the asset's current value when updating.
    /// </remarks>
    private static async Task<List<(AssetImportJobRow Row, AssetRegistration? Asset)>> CheckRowsAsync(
        AssetImportExportJob job,
        IAssetRegistrationRepository assetRepository,
        IAssetRegistrationDraftRepository draftRepository,
        CancellationToken cancellationToken)
    {
        var import = job.Import!;
        var importType = import.ImportType;
        var mapped = MappedFields(import);
        var idIndex = mapped.FirstOrDefault(map => map.Field.Key == AssetImportFields.AssetId).Index;
        var tagIndex = mapped.FirstOrDefault(map => map.Field.Key == AssetImportFields.AssetTag).Index;
        var hasId = mapped.Any(map => map.Field.Key == AssetImportFields.AssetId);
        var hasTag = mapped.Any(map => map.Field.Key == AssetImportFields.AssetTag);

        string? IdOf(AssetImportJobRow row) => hasId ? CellAt(row, idIndex) : null;
        string? TagOf(AssetImportJobRow row) => hasTag ? CellAt(row, tagIndex) : null;

        // One query for every asset the file refers to, by ID or by tag.
        var ids = import.Rows.Select(IdOf).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var tags = import.Rows.Select(TagOf).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var existingAssets = await assetRepository.FindByIdsOrTagsAsync(ids, tags, cancellationToken);
        var byId = existingAssets
            .GroupBy(asset => asset.AssetRegistrationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var byTag = existingAssets
            .Where(asset => asset.BasicInformation.AssetTag is not null)
            .GroupBy(asset => asset.BasicInformation.AssetTag!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var assetsInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagsInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(AssetImportJobRow Row, AssetRegistration? Asset)>();

        foreach (var row in import.Rows)
        {
            var issues = new List<AssetImportJobIssue>();
            var duplicate = false;
            void AddIssue(AssetImportField? field, string message, bool isDuplicate = false)
            {
                issues.Add(new AssetImportJobIssue
                {
                    Field = field?.Key,
                    Column = field is null ? null : mapped.FirstOrDefault(map => map.Field.Key == field.Key).Column,
                    Message = message
                });
                duplicate |= isDuplicate;
            }

            // Read the mapped cells as their field's type.
            var values = new List<(AssetImportField Field, object Value)>();
            var unreadable = new HashSet<string>();
            foreach (var (field, index, _) in mapped)
            {
                var text = CellAt(row, index);
                if (text is null || field.Set is null)
                {
                    continue;
                }

                var (value, error) = Parse(field, text);
                if (error is not null)
                {
                    AddIssue(field, error);
                    unreadable.Add(field.Key);
                }
                else
                {
                    values.Add((field, value!));
                }
            }

            // Find the asset the row updates.
            var idText = IdOf(row);
            var tagText = TagOf(row);
            AssetRegistration? existing = null;
            var idField = AssetImportExportFields.Find(AssetImportFields.AssetId);
            var tagField = AssetImportExportFields.Find(AssetImportFields.AssetTag);
            if (importType != AssetImportTypes.Create)
            {
                if (idText is not null)
                {
                    if (!byId.TryGetValue(idText, out existing))
                    {
                        AddIssue(idField, $"No asset with ID '{idText}' is registered");
                    }
                }
                else if (tagText is not null)
                {
                    byTag.TryGetValue(tagText, out existing);
                }

                if (existing is null && importType == AssetImportTypes.Update && idText is null)
                {
                    AddIssue(tagText is null ? idField : tagField, tagText is null
                        ? "Asset ID or Asset Tag is needed to find the asset to update"
                        : $"No asset with tag '{tagText}' is registered");
                }
            }

            var action = existing is null ? AssetImportRowActions.Create : AssetImportRowActions.Update;
            if (existing is not null && assetsInFile.TryGetValue(existing.AssetRegistrationId, out var firstRow))
            {
                AddIssue(idText is null ? tagField : idField, $"Row {firstRow} in this file already updates asset {existing.AssetRegistrationId}", isDuplicate: true);
            }
            else if (existing is not null)
            {
                assetsInFile[existing.AssetRegistrationId] = row.RowNumber;
            }

            // Apply the values to a copy of the asset (or a new one) and tidy them the way the wizard does.
            var asset = existing is null ? new AssetRegistration() : Clone(existing);
            foreach (var (field, value) in values)
            {
                field.Set!(asset, value);
            }

            Normalize(asset);

            // A row that keeps its asset's tag owns it; only rows giving out a new tag can clash, with the system or with each other.
            // Rows whose asset to update was not found are only reported as not found.
            var notFound = existing is null
                && (importType == AssetImportTypes.Update || (importType == AssetImportTypes.Upsert && idText is not null));
            var finalTag = asset.BasicInformation.AssetTag;
            var tagChanged = existing is null || !string.Equals(existing.BasicInformation.AssetTag, finalTag, StringComparison.Ordinal);
            if (finalTag is not null && tagChanged && !notFound)
            {
                if (tagsInFile.TryGetValue(finalTag, out var otherRow))
                {
                    AddIssue(tagField, $"Asset tag '{finalTag}' is also given in row {otherRow}", isDuplicate: true);
                }
                else
                {
                    tagsInFile[finalTag] = row.RowNumber;
                }

                if (byTag.TryGetValue(finalTag, out var owner) && owner.AssetRegistrationId != existing?.AssetRegistrationId)
                {
                    AddIssue(tagField, $"An asset with tag '{finalTag}' is already registered ({owner.AssetRegistrationId})", isDuplicate: true);
                }
                else if (await draftRepository.AssetTagExistsAsync(finalTag, null, cancellationToken))
                {
                    AddIssue(tagField, $"Asset tag '{finalTag}' is already used by a draft registration", isDuplicate: true);
                }
            }

            // The registration wizard's rules for a submitted asset; skipped when the asset to update was not found.
            if (!notFound)
            {
                AddValidationIssues(asset, unreadable, AddIssue);
            }

            row.Issues = issues;
            row.Action = action;
            row.AssetRegistrationId = existing?.AssetRegistrationId;
            row.AssetName = asset.BasicInformation.AssetName;
            row.AssetTag = asset.BasicInformation.AssetTag;
            row.Status = issues.Count == 0
                ? AssetImportRowStatuses.Valid
                : duplicate ? AssetImportRowStatuses.Duplicate : AssetImportRowStatuses.Invalid;

            results.Add((row, row.Status == AssetImportRowStatuses.Valid ? asset : null));
        }

        var validRows = import.Rows.Where(row => row.Status == AssetImportRowStatuses.Valid).ToList();
        import.Summary = new AssetImportJobSummary
        {
            Total = import.Rows.Count,
            Valid = validRows.Count,
            Invalid = import.Rows.Count(row => row.Status == AssetImportRowStatuses.Invalid),
            Duplicate = import.Rows.Count(row => row.Status == AssetImportRowStatuses.Duplicate),
            ToCreate = validRows.Count(row => row.Action == AssetImportRowActions.Create),
            ToUpdate = validRows.Count(row => row.Action == AssetImportRowActions.Update),
            ValidatedAt = DateTime.UtcNow
        };

        return results;
    }

    private static void AddValidationIssues(AssetRegistration asset, HashSet<string> unreadable, Action<AssetImportField?, string, bool> addIssue)
    {
        var errors = new Dictionary<string, string[]>();
        Merge(errors, AssetBasicInformationDto.FromSection(asset.BasicInformation).Validate(saveAsDraft: false));
        Merge(errors, AssetClassificationDto.FromSection(asset.Classification).Validate(saveAsDraft: false));
        Merge(errors, AssetPurchaseWarrantyDto.FromSection(asset.PurchaseWarranty).Validate(saveAsDraft: false));

        foreach (var (key, messages) in errors)
        {
            var field = AssetImportExportFields.FindByErrorKey(key);

            // A value that could not be read already has its own error, so "is required" is not added on top.
            if (field is not null && unreadable.Contains(field.Key))
            {
                continue;
            }

            foreach (var message in messages)
            {
                addIssue(field, field is not null && message.EndsWith("is required", StringComparison.Ordinal) ? $"{field.Label} is required" : message, false);
            }
        }

        // The wizard requires hierarchy ids, which a sheet cannot hold; here the level names are required instead.
        var location = asset.LocationAssignment.Location;
        RequireName("country", location.CountryName);
        RequireName("site", location.SiteName);
        RequireName("building", location.BuildingName);
        RequireName("floor", location.FloorName);
        RequireName("custodianDepartment", asset.LocationAssignment.Assignment.CustodianDepartment);

        void RequireName(string key, string? value)
        {
            var field = AssetImportExportFields.Find(key)!;
            if (value is null && !unreadable.Contains(key))
            {
                addIssue(field, $"{field.Label} is required", false);
            }
        }
    }

    private static void Merge(Dictionary<string, string[]> errors, Dictionary<string, string[]> more)
    {
        foreach (var (key, messages) in more)
        {
            errors[key] = messages;
        }
    }

    /// <summary>Converts cell text to the field's type. Lookups with a fixed list take the listed spelling; Status must be listed.</summary>
    private static (object? Value, string? Error) Parse(AssetImportField field, string text)
    {
        switch (field.DataType)
        {
            case AssetImportDataTypes.Date:
                if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return (date.Date, null);
                }

                // Excel serial dates, e.g. 45678, in a CSV or a text cell.
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 1 and < 2958466)
                {
                    return (DateTime.FromOADate(serial).Date, null);
                }

                return (null, $"'{text}' is not a valid date for {field.Label}; use e.g. 2025-01-15");

            case AssetImportDataTypes.Number:
                return decimal.TryParse(text.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    ? (number, null)
                    : (null, $"'{text}' is not a valid number for {field.Label}");

            case AssetImportDataTypes.Integer:
                return decimal.TryParse(text.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var whole)
                    && whole == Math.Floor(whole) && whole is >= int.MinValue and <= int.MaxValue
                    ? ((int)whole, null)
                    : (null, $"{field.Label} must be a whole number");

            default:
                if (field.Options is { } options)
                {
                    var listed = options.Normalize(text);
                    if (listed is null && field.Key == "status")
                    {
                        return (null, $"'{text}' is not a valid Status; use one of: {string.Join(", ", options.All)}");
                    }

                    return (listed ?? text, null);
                }

                if (field.Key == "warrantyPeriodUnit")
                {
                    return (WarrantyPeriodUnits.Normalize(text), null);
                }

                return (text, null);
        }
    }

    /// <summary>The wizard's tidy-up: trimmed text, date-only dates, derived total cost, warranty end date and location path.</summary>
    private static void Normalize(AssetRegistration asset)
    {
        asset.BasicInformation = AssetBasicInformationDto.FromSection(asset.BasicInformation)!.ToSection();
        asset.Classification = AssetClassificationDto.FromSection(asset.Classification)!.ToSection();
        asset.LocationAssignment = AssetLocationAssignmentDto.FromSection(asset.LocationAssignment)!.ToSection();
        asset.PurchaseWarranty = AssetPurchaseWarrantyDto.FromSection(asset.PurchaseWarranty)!.ToSection();
    }

    private static AssetRegistration Clone(AssetRegistration asset) =>
        BsonSerializer.Deserialize<AssetRegistration>(asset.ToBsonDocument());

    private static void MarkFailed(AssetImportJobRow row, string message)
    {
        row.Status = AssetImportRowStatuses.Failed;
        row.Issues.Add(new AssetImportJobIssue { Message = message });
    }

    private static void ResetValidation(AssetImportExportJob job, string status, int step)
    {
        var import = job.Import!;
        foreach (var row in import.Rows)
        {
            row.Status = null;
            row.Action = null;
            row.AssetRegistrationId = null;
            row.AssetName = null;
            row.AssetTag = null;
            row.Issues = [];
        }

        import.Summary = null;
        import.CurrentStep = step;
        job.Status = status;
    }

    /// <summary>The mapped fields with the index of their file column.</summary>
    private static List<(AssetImportField Field, int Index, string Column)> MappedFields(AssetImportJobDetails import) =>
        import.Mapping
            .Select(map => (Field: AssetImportExportFields.Find(map.Field), Index: import.Columns.FindIndex(column => string.Equals(column, map.Column, StringComparison.OrdinalIgnoreCase)), map.Column))
            .Where(map => map.Field is { Importable: true } && map.Index >= 0)
            .Select(map => (map.Field!, map.Index, map.Column))
            .ToList();

    private static string? CellAt(AssetImportJobRow row, int index) =>
        index >= 0 && index < row.Cells.Count ? ContractText.TrimOrNull(row.Cells[index]) : null;

    /// <summary>Labels of the mandatory fields not mapped yet, for the import type.</summary>
    private static List<string> MissingMandatory(AssetImportJobDetails import)
    {
        var mappedKeys = MappedFields(import).Select(map => map.Field.Key).ToHashSet();
        if (import.ImportType == AssetImportTypes.Update)
        {
            return mappedKeys.Contains(AssetImportFields.AssetId) || mappedKeys.Contains(AssetImportFields.AssetTag)
                ? []
                : ["Asset ID (or Asset Tag)"];
        }

        return AssetImportExportFields.Importable
            .Where(field => field.RequiredForCreate && !mappedKeys.Contains(field.Key))
            .Select(field => field.Label)
            .ToList();
    }

    /// <summary>
    /// Matches file headers to fields: first by the field's label or key, then by its alternative names. Each field is used once.
    /// The Asset ID column is left out when creating, since new assets get their ID from Asset360.
    /// </summary>
    private static Dictionary<string, string> SuggestMapping(IReadOnlyCollection<string> columns, string importType)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedFields = new HashSet<string>();
        var normalized = columns.Select(column => (Column: column, Name: AssetImportExportFields.NormalizeHeader(column))).ToList();
        var fields = AssetImportExportFields.Importable
            .Where(field => importType != AssetImportTypes.Create || field.Key != AssetImportFields.AssetId)
            .ToList();

        foreach (var useAliases in new[] { false, true })
        {
            foreach (var (column, name) in normalized.Where(column => !mapping.ContainsKey(column.Column)))
            {
                var field = fields.FirstOrDefault(field => !usedFields.Contains(field.Key) && (useAliases
                    ? field.Aliases.Contains(name)
                    : name == AssetImportExportFields.NormalizeHeader(field.Label) || name == field.Key.ToLowerInvariant()));
                if (field is not null)
                {
                    mapping[column] = field.Key;
                    usedFields.Add(field.Key);
                }
            }
        }

        return mapping;
    }

    private static AssetImportMappingResponse BuildMappingResponse(AssetImportExportJob job)
    {
        var import = job.Import!;
        var mappedBy = import.Mapping.ToDictionary(map => map.Column, map => map.Field, StringComparer.OrdinalIgnoreCase);

        var columns = import.Columns
            .Select((column, index) =>
            {
                var field = mappedBy.TryGetValue(column, out var key) ? AssetImportExportFields.Find(key) : null;
                return new AssetImportColumnMappingResponse(
                    column,
                    field?.Key,
                    field?.Label,
                    field?.DataType,
                    field?.IsMandatory(import.ImportType) ?? false,
                    import.Rows.Select(row => CellAt(row, index)).FirstOrDefault(value => value is not null),
                    field is null ? "Not Mapped" : "Mapped");
            })
            .ToList();

        var missing = MissingMandatory(import);
        return new AssetImportMappingResponse(
            AssetImportJobResponse.FromEntity(job),
            columns,
            columns.Count(column => column.MappedTo is not null),
            missing,
            missing.Count == 0,
            SuggestMapping(import.Columns, import.ImportType),
            AssetImportExportFields.Importable.Select(field => AssetImportFieldResponse.FromField(field, import.ImportType)).ToList());
    }

    private static AssetImportRecordResponse ToRecord(AssetImportJobDetails import, AssetImportJobRow row)
    {
        var values = MappedFields(import).ToDictionary(map => map.Field.Key, map => CellAt(row, map.Index));
        return new AssetImportRecordResponse(
            row.RowNumber,
            row.Status,
            row.Action,
            row.AssetRegistrationId,
            row.AssetName,
            row.AssetTag,
            values,
            row.Issues.Select(issue => new AssetImportIssueResponse(issue.Field, issue.Column, issue.Message)).ToList());
    }

    private static string IssueText(AssetImportJobIssue issue) =>
        issue.Column is null ? issue.Message : $"{issue.Column}: {issue.Message}";

    /// <summary>The sample assets of the older import template, by column label, in the order of <paramref name="fields"/>.</summary>
    private static List<IReadOnlyList<object?>> SampleRows(IReadOnlyList<AssetImportField> fields)
    {
        var headers = AssetImportColumns.All.Select(column => column.Header).ToList();
        return AssetImportSampleData.Rows
            .Select(sample => (IReadOnlyList<object?>)fields
                .Select(field =>
                {
                    var index = headers.FindIndex(header => string.Equals(header, field.Label, StringComparison.OrdinalIgnoreCase));
                    return index >= 0 && index < sample.Length ? sample[index] : null;
                })
                .ToList())
            .ToList();
    }

    private static async Task AddRelatedSheetsAsync(
        XLWorkbook workbook,
        IReadOnlyCollection<AssetRegistration> assets,
        IAssetComponentRepository componentRepository,
        IAssetAssignmentRepository assignmentRepository,
        CancellationToken cancellationToken)
    {
        var ids = assets.Select(asset => asset.AssetRegistrationId).ToList();
        var tagById = assets.GroupBy(asset => asset.AssetRegistrationId).ToDictionary(group => group.Key, group => group.First().BasicInformation.AssetTag);

        var components = await componentRepository.GetByAssetsAsync(ids, cancellationToken);
        SpreadsheetTableIO.AddSheet(workbook, "Components",
            ["Asset ID", "Asset Tag", "Component Tag", "Component Name", "Model", "Manufacturer", "Part Number", "Serial Number",
             "Installed Date", "Expected Replacement Date", "Lifecycle Used", "Rated Life", "Lifecycle Unit", "Description"],
            components
                .OrderBy(component => tagById.GetValueOrDefault(component.AssetRegistrationId))
                .ThenBy(component => component.ComponentTag)
                .Select(component => (IReadOnlyList<object?>)
                [
                    component.AssetRegistrationId, tagById.GetValueOrDefault(component.AssetRegistrationId), component.ComponentTag,
                    component.ComponentName, component.Model, component.Manufacturer, component.PartNumber, component.SerialNumber,
                    component.InstalledDate, component.ExpectedReplacementDate, component.LifecycleUsed, component.LifecycleRatedLife,
                    component.LifecycleUnit, component.Description
                ]));

        var idSet = ids.ToHashSet();
        var assignments = await assignmentRepository.GetActiveByAssetIdsAsync(ids, DateTime.UtcNow, cancellationToken);
        SpreadsheetTableIO.AddSheet(workbook, "Assignments",
            ["Asset ID", "Asset Tag", "Assignment Number", "Assignment Type", "Assigned To", "Department", "Location",
             "From Date", "To Date", "Open Ended", "Purpose", "Cost Center"],
            assignments
                .SelectMany(record => record.Assets
                    .Where(item => idSet.Contains(item.AssetRegistrationId))
                    .Select(item => (IReadOnlyList<object?>)
                    [
                        item.AssetRegistrationId, item.AssetTag ?? tagById.GetValueOrDefault(item.AssetRegistrationId), record.AssignmentNumber,
                        record.AssignmentType ?? record.AssignTo,
                        record.Employee?.EmployeeName ?? record.OtherEntity?.EntityName ?? record.Department?.DepartmentName ?? record.Location?.LocationPath,
                        record.Employee?.DepartmentName ?? record.Department?.DepartmentName,
                        record.Location?.LocationPath,
                        record.FromDate, record.ToDate, record.IsOpenEnded, record.Purpose, record.CostCenter
                    ]))
                .OrderBy(row => row[1] as string));

        SpreadsheetTableIO.AddSheet(workbook, "Specifications",
            ["Asset ID", "Asset Tag", "Specification", "Value", "Unit"],
            assets.SelectMany(asset => asset.AdditionalInformation.Technical.Specifications
                .Select(specification => (IReadOnlyList<object?>)
                [
                    asset.AssetRegistrationId, asset.BasicInformation.AssetTag, specification.Name, specification.Value, specification.Unit
                ])));

        SpreadsheetTableIO.AddSheet(workbook, "Custom Fields",
            ["Asset ID", "Asset Tag", "Field", "Type", "Value"],
            assets.SelectMany(asset => asset.AdditionalInformation.CustomFields
                .Select(customField => (IReadOnlyList<object?>)
                [
                    asset.AssetRegistrationId, asset.BasicInformation.AssetTag, customField.FieldName, customField.FieldType, customField.Value
                ])));
    }

    private static AssetExportQuery ToQuery(AssetExportFilters filters) => new(
        filters.Search,
        filters.Category,
        filters.AssetType,
        filters.Status,
        filters.LocationId,
        filters.Department,
        filters.Criticality,
        filters.Condition,
        filters.Manufacturer,
        filters.NextMaintenanceFrom,
        filters.NextMaintenanceTo);

    private static async Task<AssetImportExportJob?> FindImportAsync(string id, IAssetImportExportJobRepository repository, CancellationToken cancellationToken)
    {
        var job = await repository.GetByIdAsync(id, cancellationToken);
        return job is null || job.IsDeleted || job.Type != AssetImportExportJobRepository.ImportType || job.Import is null ? null : job;
    }

    private static async Task<bool> SaveAsync(
        AssetImportExportJob job,
        IAssetImportExportJobRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        job.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        job.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(job.Id, job, cancellationToken);
    }

    private static bool IsFinished(AssetImportExportJob job) =>
        job.Status is AssetImportExportJobRepository.CompletedStatus or AssetImportExportJobRepository.FailedStatus;

    private static (int Page, int PageSize) Paging(int page, int pageSize) =>
        (page < 1 ? 1 : page, pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize);

    private static IResult AlreadyImported(AssetImportExportJob job) =>
        Results.Conflict(new { message = $"Import {job.JobNumber} has already been imported and cannot be changed." });

    private static IResult ImportNotFound(string id) =>
        Results.NotFound(new { message = $"Import '{id}' was not found." });

    private static IResult Problem(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
