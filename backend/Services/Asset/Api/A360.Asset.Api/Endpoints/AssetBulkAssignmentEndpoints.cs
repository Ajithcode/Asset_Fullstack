using System.Globalization;
using System.Text;
using ClosedXML.Excel;
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
/// The Bulk Assignment wizard: assigns many assets at once from an Excel or CSV file.
/// </summary>
/// <remarks>
/// 1. Upload File: POST stores the file's rows as a batch (Draft) and returns its id.
/// 2. Map Columns: GET/PUT /{id}/mapping. Saving the mapping checks every row.
/// 3. Preview &amp; Validate: GET /{id}/records and /{id}/issues; Edit Data is PUT /{id}/records; Export Errors is GET /{id}/issues/export.
/// 4. Assignment Details: PUT /{id}/details sets values for rows that leave them blank, and checks the rows again.
/// 5. Confirm &amp; Assign: POST /{id}/confirm creates the assignments. Rows going to the same receiver for the same
///    period become one assignment, made the same way as in the Assign Asset wizard.
/// Rows are checked against the MasterDataManagement employees, departments, sites, buildings, floors and locations.
/// </remarks>
public static class AssetBulkAssignmentEndpoints
{
    private const string EntityType = "AssetBulkAssignment";
    private const string AssignmentEntityType = "AssetAssignment";
    private const string SequenceName = "asset-bulk-assignment";
    private const string BatchNumberPrefix = "BAS-";

    // Shared with AssetAssignmentEndpoints, so bulk and wizard assignments use one number series.
    private const string AssignmentSequenceName = "asset-assignment";
    private const string AssignmentNumberPrefix = "ASG-";

    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int PreviewRowCount = 10;
    private const int MappingPreviewRowCount = 5;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 200;

    private static readonly Dictionary<string, string> TemplateHints = new()
    {
        [AssetBulkAssignmentFields.AssetTag] = "Asset tag, or serial number when the asset has no tag",
        [AssetBulkAssignmentFields.AssetName] = "Optional; compared with the registered asset",
        [AssetBulkAssignmentFields.AssetType] = "Optional; compared with the registered asset",
        [AssetBulkAssignmentFields.AssignmentType] = "Employee, Department, Location or Other Entity",
        [AssetBulkAssignmentFields.AssignTo] = "Employee name or ID, department, place (e.g. Meeting Room 1) or entity name, by Assignment Type",
        [AssetBulkAssignmentFields.Department] = "Department name or code",
        [AssetBulkAssignmentFields.Location] = "Site, then building / floor / room separated by ' - ', e.g. Head Office - Floor 2",
        [AssetBulkAssignmentFields.StartDate] = "Date, e.g. 2025-01-15. Blank uses the date set in Assignment Details",
        [AssetBulkAssignmentFields.EndDate] = "Date, e.g. 2025-12-31. Leave blank for open ended",
        [AssetBulkAssignmentFields.Purpose] = "e.g. Operational Use",
        [AssetBulkAssignmentFields.CostCenter] = "e.g. IT-001",
        [AssetBulkAssignmentFields.Notes] = "Up to 500 characters"
    };

    public static RouteGroupBuilder MapAssetBulkAssignmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-assignments/bulk").WithTags("Asset Bulk Assignment");

        group.MapGet("/template", DownloadTemplate).WithName("DownloadAssetBulkAssignmentTemplate");
        group.MapGet("/fields", () => Results.Ok(AssetBulkAssignmentFields.All.Select(AssetBulkAssignmentFieldResponse.FromField)))
            .WithName("GetAssetBulkAssignmentFields");

        // Step 1
        group.MapPost("", UploadAsync).WithName("UploadAssetBulkAssignment").DisableAntiforgery();
        group.MapGet("/{id}", GetBatchAsync).WithName("GetAssetBulkAssignment");
        group.MapDelete("/{id}", DeleteBatchAsync).WithName("DeleteAssetBulkAssignment");
        group.MapGet("/{id}/file-rows", GetFileRowsAsync).WithName("GetAssetBulkAssignmentFileRows");

        // Step 2
        group.MapGet("/{id}/mapping", GetMappingAsync).WithName("GetAssetBulkAssignmentMapping");
        group.MapPut("/{id}/mapping", SaveMappingAsync).WithName("SaveAssetBulkAssignmentMapping");

        // Step 3
        group.MapGet("/{id}/records", GetRecordsAsync).WithName("GetAssetBulkAssignmentRecords");
        group.MapPut("/{id}/records", UpdateRecordsAsync).WithName("UpdateAssetBulkAssignmentRecords");
        group.MapPost("/{id}/validate", RevalidateAsync).WithName("ValidateAssetBulkAssignment");
        group.MapGet("/{id}/issues", GetIssuesAsync).WithName("GetAssetBulkAssignmentIssues");
        group.MapGet("/{id}/issues/export", ExportIssuesAsync).WithName("ExportAssetBulkAssignmentIssues");

        // Step 4
        group.MapPut("/{id}/details", SaveDetailsAsync).WithName("SaveAssetBulkAssignmentDetails");

        // Step 5
        group.MapPost("/{id}/confirm", ConfirmAsync).WithName("ConfirmAssetBulkAssignment");

        return group;
    }

    /// <summary>Download Template: the Assignments sheet (with sample rows unless includeSampleData=false) and an Instructions sheet.</summary>
    private static IResult DownloadTemplate(bool includeSampleData = true)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(AssetBulkAssignmentFields.SheetName);
        var fields = AssetBulkAssignmentFields.All;

        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            var cell = sheet.Cell(1, index + 1);
            cell.Value = field.Required ? $"{field.Label} *" : field.Label;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = field.Required ? XLColor.FromHtml("#E9E5FF") : XLColor.FromHtml("#F2F2F2");
            cell.CreateComment().AddText(TemplateHints[field.Key]);
        }

        if (includeSampleData)
        {
            for (var row = 0; row < AssetBulkAssignmentSampleData.Rows.Count; row++)
            {
                var values = AssetBulkAssignmentSampleData.Rows[row];
                for (var index = 0; index < values.Length; index++)
                {
                    if (values[index] is not { } text)
                    {
                        continue;
                    }

                    var cell = sheet.Cell(row + 2, index + 1);
                    if (fields[index].DataType == "Date")
                    {
                        cell.Value = DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                    }
                    else
                    {
                        cell.Value = text;
                    }
                }
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        var instructions = workbook.AddWorksheet("Instructions");
        instructions.Cell(1, 1).Value = "Column";
        instructions.Cell(1, 2).Value = "Required";
        instructions.Cell(1, 3).Value = "Notes";
        instructions.Row(1).Style.Font.Bold = true;
        for (var index = 0; index < fields.Count; index++)
        {
            instructions.Cell(index + 2, 1).Value = fields[index].Label;
            instructions.Cell(index + 2, 2).Value = fields[index].Required ? "Yes" : "No";
            instructions.Cell(index + 2, 3).Value = TemplateHints[fields[index].Key];
        }

        var notesRow = fields.Count + 3;
        instructions.Cell(notesRow, 1).Value = $"Fill one asset per row on the '{AssetBulkAssignmentFields.SheetName}' sheet, starting from row 2. Keep the header row.";
        instructions.Cell(notesRow + 1, 1).Value = $"At most {AssetBulkAssignmentFields.MaxRows} rows per file. Blank values can be set for all rows in the Assignment Details step.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return Results.File(stream.ToArray(), ExcelContentType, "asset-bulk-assignment-template.xlsx");
    }

    /// <summary>Step 1: upload the file as form field "file" (.xlsx or .csv, at most 10 MB). The first row must be the header row.</summary>
    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        IAssetBulkAssignmentRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FileProblem("Upload an Excel or CSV file");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return FileProblem(extension == ".xls"
                ? "Old .xls files cannot be read; save the file as .xlsx and upload it again"
                : "Only .xlsx and .csv files can be uploaded");
        }

        if (file.Length > AssetBulkAssignmentFields.MaxFileSizeBytes)
        {
            return FileProblem("File cannot be larger than 10 MB");
        }

        ParsedFile parsed;
        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;
            parsed = extension == ".csv" ? ReadCsv(stream) : ReadWorkbook(stream);
        }
        catch (Exception)
        {
            return FileProblem(extension == ".csv" ? "The file could not be read as CSV" : "The file could not be read as an Excel workbook");
        }

        if (parsed.Columns.Count == 0)
        {
            return FileProblem("The first row must be the header row with the column names");
        }

        if (parsed.Columns.Count > AssetBulkAssignmentFields.MaxColumns)
        {
            return FileProblem($"The file cannot have more than {AssetBulkAssignmentFields.MaxColumns} columns");
        }

        if (parsed.Rows.Count == 0)
        {
            return FileProblem("The file has no data rows");
        }

        if (parsed.Rows.Count > AssetBulkAssignmentFields.MaxRows)
        {
            return FileProblem($"At most {AssetBulkAssignmentFields.MaxRows} rows can be assigned at once; the file has {parsed.Rows.Count}");
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var currentUser = currentUserAccessor.GetCurrentUser();

        var batch = new AssetBulkAssignment
        {
            BatchNumber = $"{BatchNumberPrefix}{nextSequence:D6}",
            CurrentStep = 1,
            FileName = Path.GetFileName(file.FileName),
            FileSize = file.Length,
            Columns = parsed.Columns,
            EmptyRowCount = parsed.EmptyRowCount,
            Rows = parsed.Rows,
            Status = AssetBulkAssignmentRepository.DraftStatus,
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };

        await repository.CreateAsync(batch, cancellationToken);
        await eventLogger.LogAsync(EntityType, batch.Id, $"{batch.BatchNumber} - {batch.FileName} uploaded", EventAction.Created, cancellationToken);

        var suggestedMapping = SuggestMapping(batch.Columns);
        var missing = AssetBulkAssignmentFields.All
            .Where(field => field.Required && !suggestedMapping.ContainsKey(field.Key))
            .Select(field => field.Label)
            .ToList();

        var fileValidations = new List<AssetBulkAssignmentFileCheck>
        {
            new("File format is valid", true, null),
            new("Required columns found", missing.Count == 0,
                missing.Count == 0 ? null : $"Not found by name: {string.Join(", ", missing)}. Map them in the next step."),
            new("No empty rows", batch.EmptyRowCount == 0,
                batch.EmptyRowCount == 0 ? null : $"{batch.EmptyRowCount} empty rows were skipped"),
            new("Data read successfully", true, $"{batch.Rows.Count} records read")
        };

        return Results.Created(
            $"/api/asset-assignments/bulk/{batch.Id}",
            new AssetBulkAssignmentUploadResponse(
                AssetBulkAssignmentResponse.FromEntity(batch),
                fileValidations,
                batch.Rows.Take(PreviewRowCount).Select(ToFileRow).ToList(),
                suggestedMapping));
    }

    private static async Task<IResult> GetBatchAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        return batch is null ? BatchNotFound(id) : Results.Ok(AssetBulkAssignmentResponse.FromEntity(batch));
    }

    /// <summary>Cancel: removes a batch that has not been confirmed. A confirmed batch is kept as the record of what was assigned.</summary>
    private static async Task<IResult> DeleteBatchAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return BatchNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, batch.Id, $"{batch.BatchNumber} - {batch.FileName} cancelled", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>View All on step 1: the rows as read from the file.</summary>
    private static async Task<IResult> GetFileRowsAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        (page, pageSize) = Paging(page, pageSize);
        return Results.Ok(new AssetBulkAssignmentFileRowsResponse(
            batch.Columns,
            batch.Rows.Skip((page - 1) * pageSize).Take(pageSize).Select(ToFileRow).ToList(),
            batch.Rows.Count,
            page,
            pageSize));
    }

    private static async Task<IResult> GetMappingAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        return batch is null ? BatchNotFound(id) : Results.Ok(BuildMappingResponse(batch));
    }

    /// <summary>
    /// Step 2 Next: saves the mapping, takes each row's values from the mapped columns and checks every row.
    /// Saving it again starts the rows over from the file, so earlier Edit Data changes are dropped.
    /// </summary>
    private static async Task<IResult> SaveMappingAsync(
        string id,
        SaveAssetBulkAssignmentMappingRequest request,
        IAssetBulkAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        var errors = new Dictionary<string, string[]>();
        var mapping = new List<AssetBulkAssignmentColumnMap>();
        var usedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, column) in request.Mapping ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                continue;
            }

            var field = AssetBulkAssignmentFields.Find(key);
            if (field is null)
            {
                errors[$"Mapping.{key}"] = [$"'{key}' is not a bulk assignment field"];
                continue;
            }

            var fileColumn = batch.Columns.FirstOrDefault(name => string.Equals(name, column.Trim(), StringComparison.OrdinalIgnoreCase));
            if (fileColumn is null)
            {
                errors[$"Mapping.{field.Key}"] = [$"Column '{column}' is not in the file"];
            }
            else if (!usedColumns.Add(fileColumn))
            {
                errors[$"Mapping.{field.Key}"] = [$"Column '{fileColumn}' is mapped to more than one field"];
            }
            else
            {
                mapping.Add(new AssetBulkAssignmentColumnMap { Field = field.Key, Column = fileColumn });
            }
        }

        foreach (var field in AssetBulkAssignmentFields.All.Where(field => field.Required))
        {
            if (mapping.All(map => map.Field != field.Key) && !errors.ContainsKey($"Mapping.{field.Key}"))
            {
                errors[$"Mapping.{field.Key}"] = [$"Map a column to {field.Label}"];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        batch.Mapping = mapping;
        foreach (var row in batch.Rows)
        {
            row.Values = MapRow(batch.Columns, row.Cells, mapping);
            row.IsEdited = false;
        }

        await AssetBulkAssignmentRowChecker.CheckAsync(batch, assetRepository, assignmentRepository, masterDataRepository, cancellationToken);
        batch.CurrentStep = Math.Max(batch.CurrentStep, 3);

        if (!await SaveAsync(batch, repository, currentUserAccessor, cancellationToken))
        {
            return BatchNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, batch.Id, $"{batch.BatchNumber} - columns mapped", EventAction.Updated, cancellationToken);
        return Results.Ok(BuildMappingResponse(batch));
    }

    /// <summary>
    /// Asset Records (step 3) and Preview of Valid Records (step 5).
    /// Search matches asset tag, name, type, receiver, department, location and issue text; Status is Valid, Warning or Error.
    /// </summary>
    private static async Task<IResult> GetRecordsAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        var records = batch.Rows.OrderBy(row => row.RowNumber).Select(AssetBulkAssignmentRecordResponse.FromRow);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = AssetBulkAssignmentRowStatuses.Normalize(status);
            if (wanted is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Status"] = [$"Status must be one of: {string.Join(", ", AssetBulkAssignmentRowStatuses.All)}"]
                });
            }

            records = records.Where(record => record.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            records = records.Where(record =>
                new[] { record.AssetTag, record.AssetName, record.AssetType, record.AssignTo, record.Department, record.Location, record.Values.AssetTag }
                    .Concat(record.Issues.Select(issue => issue.Message))
                    .Any(value => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true));
        }

        var filtered = records.ToList();
        (page, pageSize) = Paging(page, pageSize);

        return Results.Ok(new AssetBulkAssignmentRecordsResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            filtered.Count,
            AssetBulkAssignmentCounts.FromRows(batch.Rows),
            page,
            pageSize));
    }

    /// <summary>Edit Data: replaces the values of the given rows and checks every row again (a fix can clear a duplicate elsewhere).</summary>
    private static async Task<IResult> UpdateRecordsAsync(
        string id,
        UpdateAssetBulkAssignmentRowsRequest request,
        IAssetBulkAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        if (batch.Mapping.Count == 0)
        {
            return MapColumnsFirst();
        }

        var edits = request.Rows ?? [];
        if (edits.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Rows"] = ["Send at least one row"]
            });
        }

        var errors = new Dictionary<string, string[]>();
        var editedRows = new List<AssetBulkAssignmentRow>();
        var index = 0;
        foreach (var edit in edits)
        {
            var row = batch.Rows.FirstOrDefault(item => item.RowNumber == edit.RowNumber);
            if (row is null)
            {
                errors[$"Rows[{index}].RowNumber"] = [$"Row {edit.RowNumber} is not in the file"];
            }
            else if (edit.Values is null)
            {
                errors[$"Rows[{index}].Values"] = ["Values are required"];
            }
            else
            {
                row.Values = edit.Values.ToEntity();
                row.IsEdited = true;
                editedRows.Add(row);
            }

            index++;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await AssetBulkAssignmentRowChecker.CheckAsync(batch, assetRepository, assignmentRepository, masterDataRepository, cancellationToken);

        if (!await SaveAsync(batch, repository, currentUserAccessor, cancellationToken))
        {
            return BatchNotFound(id);
        }

        await eventLogger.LogAsync(
            EntityType,
            batch.Id,
            $"{batch.BatchNumber} - rows edited: {string.Join(", ", editedRows.Select(row => row.RowNumber))}",
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(new AssetBulkAssignmentRecordsResponse(
            editedRows.Select(AssetBulkAssignmentRecordResponse.FromRow).ToList(),
            editedRows.Count,
            AssetBulkAssignmentCounts.FromRows(batch.Rows),
            1,
            editedRows.Count));
    }

    /// <summary>Checks every row again, e.g. after an employee or location was added to the master data.</summary>
    private static async Task<IResult> RevalidateAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        if (batch.Mapping.Count == 0)
        {
            return MapColumnsFirst();
        }

        await AssetBulkAssignmentRowChecker.CheckAsync(batch, assetRepository, assignmentRepository, masterDataRepository, cancellationToken);

        return await SaveAsync(batch, repository, currentUserAccessor, cancellationToken)
            ? Results.Ok(AssetBulkAssignmentResponse.FromEntity(batch))
            : BatchNotFound(id);
    }

    /// <summary>The Errors &amp; Warnings panel; Severity is Error or Warning (the tabs), empty for all.</summary>
    private static async Task<IResult> GetIssuesAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken,
        string? severity = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        var issues = batch.Rows
            .OrderBy(row => row.RowNumber)
            .SelectMany(row => row.Issues
                .OrderBy(issue => issue.Severity == AssetBulkAssignmentSeverities.Error ? 0 : 1)
                .Select(issue => new AssetBulkAssignmentIssueResponse(
                    row.RowNumber,
                    row.Resolved?.AssetTag ?? row.Values.AssetTag,
                    issue.Field,
                    AssetBulkAssignmentFields.LabelFor(issue.Field),
                    issue.Severity,
                    issue.Message,
                    issue.Details)))
            .ToList();

        var filtered = string.IsNullOrWhiteSpace(severity)
            ? issues
            : issues.Where(issue => string.Equals(issue.Severity, severity.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        (page, pageSize) = Paging(page, pageSize);

        return Results.Ok(new AssetBulkAssignmentIssuesResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            filtered.Count,
            issues.Count(issue => issue.Severity == AssetBulkAssignmentSeverities.Error),
            issues.Count(issue => issue.Severity == AssetBulkAssignmentSeverities.Warning),
            page,
            pageSize));
    }

    /// <summary>
    /// Export Errors: an Excel file of the rows with errors (and warnings unless includeWarnings=false), with the
    /// template's columns, so it can be fixed and uploaded again.
    /// </summary>
    private static async Task<IResult> ExportIssuesAsync(
        string id,
        IAssetBulkAssignmentRepository repository,
        CancellationToken cancellationToken,
        bool includeWarnings = true)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        var rows = batch.Rows
            .Where(row => row.Status == AssetBulkAssignmentRowStatuses.Error
                || (includeWarnings && row.Status == AssetBulkAssignmentRowStatuses.Warning))
            .OrderBy(row => row.RowNumber)
            .ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(AssetBulkAssignmentFields.SheetName);
        var fields = AssetBulkAssignmentFields.All;

        sheet.Cell(1, 1).Value = "Row";
        for (var index = 0; index < fields.Count; index++)
        {
            sheet.Cell(1, index + 2).Value = fields[index].Required ? $"{fields[index].Label} *" : fields[index].Label;
        }

        var statusColumn = fields.Count + 2;
        sheet.Cell(1, statusColumn).Value = "Status";
        sheet.Cell(1, statusColumn + 1).Value = "Issues";
        sheet.Row(1).Style.Font.Bold = true;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var sheetRow = rowIndex + 2;

            sheet.Cell(sheetRow, 1).Value = row.RowNumber;
            for (var index = 0; index < fields.Count; index++)
            {
                sheet.Cell(sheetRow, index + 2).Value = fields[index].Get(row.Values) ?? string.Empty;
            }

            var statusCell = sheet.Cell(sheetRow, statusColumn);
            statusCell.Value = row.Status;
            statusCell.Style.Fill.BackgroundColor = row.Status == AssetBulkAssignmentRowStatuses.Error
                ? XLColor.FromHtml("#FDE2E1")
                : XLColor.FromHtml("#FFF4D6");

            sheet.Cell(sheetRow, statusColumn + 1).Value = string.Join("; ", row.Issues.Select(issue =>
                $"{issue.Severity}: {AssetBulkAssignmentFields.LabelFor(issue.Field)} - {issue.Details ?? issue.Message}"));
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return Results.File(stream.ToArray(), ExcelContentType, $"{batch.BatchNumber}-errors.xlsx");
    }

    /// <summary>Step 4 Next: saves the common details and checks every row again with them.</summary>
    private static async Task<IResult> SaveDetailsAsync(
        string id,
        SaveAssetBulkAssignmentDetailsRequest request,
        IAssetBulkAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        if (batch.Mapping.Count == 0)
        {
            return MapColumnsFirst();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        batch.CommonDetails = request.ToEntity();
        await AssetBulkAssignmentRowChecker.CheckAsync(batch, assetRepository, assignmentRepository, masterDataRepository, cancellationToken);
        batch.CurrentStep = Math.Max(batch.CurrentStep, 4);

        if (!await SaveAsync(batch, repository, currentUserAccessor, cancellationToken))
        {
            return BatchNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, batch.Id, $"{batch.BatchNumber} - assignment details saved", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetBulkAssignmentResponse.FromEntity(batch));
    }

    /// <summary>
    /// Step 5 Confirm &amp; Assign. Every row is checked once more; rows with errors are skipped, and so are assets
    /// already assigned unless Override existing assignment was ticked (then they are taken out of their current assignment).
    /// Rows going to the same receiver, location and period become one assignment.
    /// SendNotifications switches the step 4 notification options on; with none ticked, assignees are notified.
    /// </summary>
    private static async Task<IResult> ConfirmAsync(
        string id,
        ConfirmAssetBulkAssignmentRequest? request,
        IAssetBulkAssignmentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var batch = await FindAsync(id, repository, cancellationToken);
        if (batch is null)
        {
            return BatchNotFound(id);
        }

        if (IsCompleted(batch))
        {
            return AlreadyCompleted(batch);
        }

        if (batch.Mapping.Count == 0)
        {
            return MapColumnsFirst();
        }

        var check = await AssetBulkAssignmentRowChecker.CheckAsync(batch, assetRepository, assignmentRepository, masterDataRepository, cancellationToken);
        var common = batch.CommonDetails;
        var overrideExisting = common?.OverrideExistingAssignment == true;

        var assignable = new List<AssetBulkAssignmentRow>();
        foreach (var row in batch.Rows.OrderBy(row => row.RowNumber))
        {
            row.AssignmentId = null;
            row.AssignmentNumber = null;
            row.SkippedReason = null;

            if (row.Status == AssetBulkAssignmentRowStatuses.Error)
            {
                var error = row.Issues.First(issue => issue.Severity == AssetBulkAssignmentSeverities.Error);
                row.SkippedReason = error.Details ?? error.Message;
            }
            else if (row.Resolved?.CurrentAssignmentNumber is { } current && !overrideExisting)
            {
                row.SkippedReason = $"Already assigned in {current}";
            }
            else
            {
                assignable.Add(row);
            }
        }

        if (assignable.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Rows"] = ["No rows can be assigned; fix the errors or tick Override existing assignment"]
            });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var sendNotifications = request?.SendNotifications ?? false;

        // Assets being reassigned keep the status they had before their first assignment, so releasing them later restores it.
        var previousStatuses = await ReleaseOverriddenAssetsAsync(
            assignable,
            check.ActiveAssignments,
            batch,
            currentUser?.UserName,
            assignmentRepository,
            eventLogger,
            cancellationToken);

        var created = new List<AssetBulkAssignmentCreatedAssignment>();
        foreach (var group in assignable.GroupBy(row => GroupKey(row.Resolved!), StringComparer.OrdinalIgnoreCase))
        {
            var rows = group.ToList();
            var resolved = rows[0].Resolved!;
            var assets = rows.Select(row => check.Assets[row.Resolved!.AssetRegistrationId!]).ToList();

            var nextSequence = await sequenceGenerator.GetNextValueAsync(AssignmentSequenceName, cancellationToken);
            var assignment = new AssetAssignmentRecord
            {
                AssignmentNumber = $"{AssignmentNumberPrefix}{nextSequence:D6}",
                SelectionMode = AssetAssignmentSelectionModes.BulkUpload,
                BulkAssignmentId = batch.Id,
                Assets = assets.Select(asset =>
                {
                    var copy = AssetAssignmentAssets.FromAsset(asset);
                    if (previousStatuses.TryGetValue(asset.AssetRegistrationId, out var previousStatus))
                    {
                        copy.PreviousStatus = previousStatus;
                    }

                    return copy;
                }).ToList(),
                AssignTo = resolved.AssignTo!,
                AssignmentType = common?.AssignmentReason,
                FromDate = resolved.StartDate,
                ToDate = resolved.EndDate,
                IsOpenEnded = resolved.EndDate is null,
                Employee = resolved.AssignTo == AssetAssignmentTargets.Employee ? resolved.Employee : null,
                Department = resolved.Department,
                OtherEntity = resolved.AssignTo == AssetAssignmentTargets.OtherEntity ? ToOtherEntity(resolved, common) : null,
                Location = resolved.Location,
                CostCenter = resolved.CostCenter,
                Purpose = resolved.Purpose,
                ApprovalReference = common?.Reference,
                Notes = resolved.Notes,
                Notification = ToNotification(common, sendNotifications),
                CustomFields = new AssetAssignmentCustomFields
                {
                    AssignmentReference = common?.Reference,
                    AssignmentReason = common?.AssignmentReason
                },
                Status = AssetAssignmentRepository.ActiveStatus,
                CreatedBy = currentUser?.UserName,
                CreatedAt = DateTime.UtcNow,
                ClientId = batch.ClientId ?? currentUser?.ClientId,
                TenantId = batch.TenantId ?? currentUser?.TenantId,
                IsDeleted = false
            };

            await assignmentRepository.CreateAsync(assignment, cancellationToken);

            var assignedToName = AssetAssignmentAssets.AssignedToName(assignment);
            await eventLogger.LogAsync(
                AssignmentEntityType,
                assignment.Id,
                $"{assignment.AssignmentNumber} - {assignment.Assets.Count} assets to {assignment.AssignTo} {assignedToName} (bulk {batch.BatchNumber})".TrimEnd(),
                EventAction.Created,
                cancellationToken);

            await AssetAssignmentEndpoints.AssignAssetsAsync(assets, assignment, currentUser?.UserName, assetRepository, eventLogger, cancellationToken);

            foreach (var row in rows)
            {
                row.AssignmentId = assignment.Id;
                row.AssignmentNumber = assignment.AssignmentNumber;
            }

            created.Add(new AssetBulkAssignmentCreatedAssignment(
                assignment.Id,
                assignment.AssignmentNumber,
                assignment.AssignTo,
                assignedToName,
                assignment.Assets.Count));
        }

        var skippedRows = batch.Rows
            .Where(row => row.SkippedReason is not null)
            .OrderBy(row => row.RowNumber)
            .Select(row => new AssetBulkAssignmentSkippedRow(row.RowNumber, row.Resolved?.AssetTag ?? row.Values.AssetTag, row.SkippedReason!))
            .ToList();

        batch.Result = new AssetBulkAssignmentResult
        {
            CompletedAt = DateTime.UtcNow,
            CompletedBy = currentUser?.UserName,
            SendNotifications = sendNotifications,
            AssignedCount = assignable.Count,
            SkippedCount = skippedRows.Count,
            AssignmentIds = created.Select(assignment => assignment.Id).ToList()
        };
        batch.Status = AssetBulkAssignmentRepository.CompletedStatus;
        batch.CurrentStep = 5;

        await SaveAsync(batch, repository, currentUserAccessor, cancellationToken);
        await eventLogger.LogAsync(
            EntityType,
            batch.Id,
            $"{batch.BatchNumber} - {assignable.Count} assets assigned in {created.Count} assignments, {skippedRows.Count} rows skipped",
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(new AssetBulkAssignmentConfirmResponse(
            AssetBulkAssignmentResponse.FromEntity(batch),
            assignable.Count,
            skippedRows.Count,
            created,
            skippedRows));
    }

    /// <summary>
    /// Takes the reassigned assets out of the active assignments they are in. An assignment left with no assets ends today.
    /// Returns each released asset's status from before that assignment.
    /// </summary>
    private static async Task<Dictionary<string, string?>> ReleaseOverriddenAssetsAsync(
        IReadOnlyCollection<AssetBulkAssignmentRow> assignable,
        IReadOnlyCollection<AssetAssignmentRecord> activeAssignments,
        AssetBulkAssignment batch,
        string? userName,
        IAssetAssignmentRepository assignmentRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var previousStatuses = new Dictionary<string, string?>(StringComparer.Ordinal);
        var reassigned = assignable
            .Where(row => row.Resolved?.CurrentAssignmentNumber is not null)
            .Select(row => row.Resolved!.AssetRegistrationId!)
            .ToHashSet(StringComparer.Ordinal);

        if (reassigned.Count == 0)
        {
            return previousStatuses;
        }

        foreach (var assignment in activeAssignments)
        {
            var released = assignment.Assets.Where(asset => reassigned.Contains(asset.AssetRegistrationId)).ToList();
            if (released.Count == 0)
            {
                continue;
            }

            foreach (var asset in released)
            {
                previousStatuses.TryAdd(asset.AssetRegistrationId, asset.PreviousStatus);
            }

            assignment.Assets = assignment.Assets.Where(asset => !reassigned.Contains(asset.AssetRegistrationId)).ToList();
            if (assignment.Assets.Count == 0)
            {
                var today = DateTime.UtcNow.Date;
                assignment.IsOpenEnded = false;
                assignment.ToDate = assignment.FromDate > today ? assignment.FromDate : today;
            }

            assignment.UpdatedBy = userName;
            assignment.UpdatedAt = DateTime.UtcNow;

            await assignmentRepository.UpdateAsync(assignment.Id, assignment, cancellationToken);
            await eventLogger.LogAsync(
                AssignmentEntityType,
                assignment.Id,
                $"{assignment.AssignmentNumber} - {released.Count} assets reassigned by bulk {batch.BatchNumber}",
                EventAction.Updated,
                cancellationToken);
        }

        return previousStatuses;
    }

    /// <summary>Rows with the same receiver, location, period and texts go into one assignment.</summary>
    private static string GroupKey(AssetBulkAssignmentResolved resolved) => string.Join("|",
        resolved.AssignTo,
        resolved.Employee?.EmployeeId,
        resolved.Employee?.DepartmentId,
        resolved.Department?.DepartmentId,
        resolved.OtherEntityName,
        resolved.Location?.SiteId,
        resolved.Location?.LocationPath,
        resolved.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        resolved.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        resolved.Purpose,
        resolved.CostCenter,
        resolved.Notes);

    /// <summary>The common entity's details when the row names the same entity; otherwise just the name.</summary>
    private static AssetAssignmentOtherEntity ToOtherEntity(AssetBulkAssignmentResolved resolved, AssetBulkAssignmentCommonDetails? common)
    {
        if (common?.OtherEntity is { } entity && string.Equals(entity.EntityName, resolved.OtherEntityName, StringComparison.OrdinalIgnoreCase))
        {
            return entity;
        }

        return new AssetAssignmentOtherEntity
        {
            EntityType = common?.OtherEntity?.EntityType ?? AssetAssignmentOtherEntityTypes.Normalize(null),
            EntityName = resolved.OtherEntityName ?? string.Empty
        };
    }

    private static AssetAssignmentNotification ToNotification(AssetBulkAssignmentCommonDetails? common, bool sendNotifications)
    {
        if (!sendNotifications)
        {
            return new AssetAssignmentNotification();
        }

        var anyChosen = common is not null && (common.NotifyAssignees || common.NotifyDepartmentManager || common.NotifyDepartment);
        return new AssetAssignmentNotification
        {
            NotifyAssignees = !anyChosen || common!.NotifyAssignees,
            NotifyManager = anyChosen && common!.NotifyDepartmentManager,
            NotifyDepartment = anyChosen && common!.NotifyDepartment
        };
    }

    private static AssetBulkAssignmentMappingResponse BuildMappingResponse(AssetBulkAssignment batch)
    {
        var suggested = SuggestMapping(batch.Columns);
        var mapping = batch.Mapping.Count > 0
            ? batch.Mapping.ToDictionary(map => map.Field, map => map.Column)
            : suggested;
        var mappedBy = mapping.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        var maps = mapping.Select(pair => new AssetBulkAssignmentColumnMap { Field = pair.Key, Column = pair.Value }).ToList();

        var fileColumns = batch.Columns
            .Select((column, index) => new AssetBulkAssignmentFileColumn(
                column,
                batch.Rows.Select(row => index < row.Cells.Count ? row.Cells[index] : null).FirstOrDefault(value => value is not null),
                mappedBy.GetValueOrDefault(column)))
            .ToList();

        var preview = batch.Rows
            .Take(MappingPreviewRowCount)
            .Select(row => new AssetBulkAssignmentMappedRow(
                row.RowNumber,
                AssetBulkAssignmentValuesDto.FromEntity(batch.Mapping.Count > 0 ? row.Values : MapRow(batch.Columns, row.Cells, maps))))
            .ToList();

        return new AssetBulkAssignmentMappingResponse(
            AssetBulkAssignmentResponse.FromEntity(batch),
            fileColumns,
            AssetBulkAssignmentFields.All.Select(AssetBulkAssignmentFieldResponse.FromField).ToList(),
            mapping,
            suggested,
            preview);
    }

    /// <summary>Matches file headers to fields: first by the field's own name, then by its aliases. Each column is used once.</summary>
    private static Dictionary<string, string> SuggestMapping(IReadOnlyCollection<string> columns)
    {
        var mapping = new Dictionary<string, string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = columns.Select(column => (Column: column, Name: AssetBulkAssignmentFields.NormalizeHeader(column))).ToList();

        foreach (var useAliases in new[] { false, true })
        {
            foreach (var field in AssetBulkAssignmentFields.All.Where(field => !mapping.ContainsKey(field.Key)))
            {
                var names = useAliases
                    ? field.Aliases
                    : [AssetBulkAssignmentFields.NormalizeHeader(field.Label), field.Key.ToLowerInvariant()];

                var match = normalized.FirstOrDefault(column => !used.Contains(column.Column) && names.Contains(column.Name));
                if (match.Column is not null)
                {
                    mapping[field.Key] = match.Column;
                    used.Add(match.Column);
                }
            }
        }

        return mapping;
    }

    private static AssetBulkAssignmentValues MapRow(IReadOnlyList<string> columns, IReadOnlyList<string?> cells, IEnumerable<AssetBulkAssignmentColumnMap> mapping)
    {
        var values = new AssetBulkAssignmentValues();
        foreach (var map in mapping)
        {
            var index = columns.ToList().FindIndex(column => string.Equals(column, map.Column, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index < cells.Count)
            {
                AssetBulkAssignmentFields.Find(map.Field)?.Set(values, ContractText.TrimOrNull(cells[index]));
            }
        }

        return values;
    }

    private static ParsedFile ReadWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(worksheet => string.Equals(worksheet.Name, AssetBulkAssignmentFields.SheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var lastColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        var records = Enumerable.Range(1, lastRow)
            .Select(rowNumber => (rowNumber, Enumerable.Range(1, lastColumn).Select(column => CellText(sheet.Cell(rowNumber, column))).ToList()));

        return ToParsedFile(records);
    }

    /// <summary>Reads a CSV file (comma, semicolon or tab separated; quoted values may hold separators and line breaks).</summary>
    private static ParsedFile ReadCsv(Stream stream)
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

        return ToParsedFile(records);
    }

    /// <summary>
    /// The first record is the header row. Blank rows are skipped, and those between data rows are counted.
    /// Headers lose a trailing "*"; blank headers become "Column N" and repeated ones get " (2)", " (3)"…
    /// </summary>
    private static ParsedFile ToParsedFile(IEnumerable<(int RowNumber, List<string?> Cells)> records)
    {
        var columns = new List<string>();
        var rows = new List<AssetBulkAssignmentRow>();
        var emptyRows = 0;
        var pendingEmptyRows = 0;
        var isHeader = true;

        foreach (var (rowNumber, cells) in records)
        {
            if (isHeader)
            {
                isHeader = false;
                var lastHeader = cells.FindLastIndex(cell => cell is not null);
                for (var index = 0; index <= lastHeader; index++)
                {
                    var header = cells[index]?.TrimEnd('*').Trim();
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
            rows.Add(new AssetBulkAssignmentRow { RowNumber = rowNumber, Cells = values });
        }

        return new ParsedFile(columns, rows, emptyRows);
    }

    private static string? CellText(IXLCell cell)
    {
        if (cell.Value.IsBlank)
        {
            return null;
        }

        // Dates come back as the date, not the serial number.
        var text = cell.Value.IsDateTime
            ? cell.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : cell.GetString();
        return ContractText.TrimOrNull(text);
    }

    private static AssetBulkAssignmentFileRow ToFileRow(AssetBulkAssignmentRow row) => new(row.RowNumber, row.Cells);

    private static async Task<AssetBulkAssignment?> FindAsync(string id, IAssetBulkAssignmentRepository repository, CancellationToken cancellationToken)
    {
        var batch = await repository.GetByIdAsync(id, cancellationToken);
        return batch is null || batch.IsDeleted ? null : batch;
    }

    private static async Task<bool> SaveAsync(
        AssetBulkAssignment batch,
        IAssetBulkAssignmentRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        batch.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        batch.UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(batch.Id, batch, cancellationToken);
    }

    private static (int Page, int PageSize) Paging(int page, int pageSize) =>
        (page < 1 ? 1 : page, pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize);

    private static bool IsCompleted(AssetBulkAssignment batch) =>
        string.Equals(batch.Status, AssetBulkAssignmentRepository.CompletedStatus, StringComparison.Ordinal);

    private static IResult AlreadyCompleted(AssetBulkAssignment batch) =>
        Results.Conflict(new { message = $"Bulk assignment {batch.BatchNumber} is already confirmed and cannot be changed." });

    private static IResult MapColumnsFirst() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Mapping"] = ["Map the file columns first"]
        });

    private static IResult FileProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["File"] = [message]
        });

    private static IResult BatchNotFound(string id) =>
        Results.NotFound(new { message = $"Bulk assignment '{id}' was not found." });

    private sealed record ParsedFile(List<string> Columns, List<AssetBulkAssignmentRow> Rows, int EmptyRowCount);
}
