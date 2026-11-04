import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import * as XLSX from 'xlsx';
import { environment } from 'shared-ui';
import { apiErrorMessage, toParams } from './analytics/asset-performance/kit/api-utils';

export type ReportStatus = 'Completed' | 'Failed';
export type ReportCell = string | number | null;

/** A row of the Generated Reports list (GET /api/generated-reports): no row data. */
export interface Report {
  id: string;
  slNo: number;
  title: string;
  module: string;
  template: string;
  datasetId: string;
  timeRange: string;
  expireOn: string;
  recurrence: string;
  createdOn: string;
  type: string;
  shareWith: string;
  generatedOn: string;
  status: ReportStatus;
  error: string | null;
  rowCount: number;
  truncated: boolean;
}

/** A report with the column headers and rows read from its dataset when it was (re)generated. */
export interface GeneratedReport extends Report {
  headers: string[];
  rows: Record<string, ReportCell>[];
}

export interface ReportTemplate {
  id: string;
  name: string;
  datasetId: string | null;
  datasetName: string | null;
  columns: string[];
  dateField: string | null;
  available: boolean;
  unavailableReason: string | null;
  note: string | null;
}

export interface ReportModule {
  id: string;
  name: string;
  templates: ReportTemplate[];
}

export interface ReportTemplateCatalog {
  modules: ReportModule[];
  timeRanges: { value: string; label: string }[];
  recurrences: string[];
  formats: string[];
  weekdays: string[];
  maxRows: number;
}

/** Create Report form, as POST /api/generated-reports expects it. */
export interface CreateReportForm {
  name: string;
  module: string;
  template: string;
  timeRange: string;
  recurrence: string;
  scheduleTime?: string | null;
  scheduleDay?: string | null;
  scheduleDate?: string | null;
  format: string;
  shareWith: string[];
  expireOn?: string | null;
}

// ----- API shapes -----

interface ReportSummaryDto {
  id: string;
  name: string;
  module: string;
  templateId: string;
  template: string;
  datasetId: string;
  timeRange: string;
  timeRangeLabel: string;
  recurrence: string;
  format: string;
  shareWith: string[];
  expireOn: string | null;
  createdOn: string | null;
  generatedOn: string | null;
  status: ReportStatus;
  error: string | null;
  rowCount: number;
  truncated: boolean;
}

interface ReportDto {
  report: ReportSummaryDto;
  headers: string[];
  rows: ReportCell[][];
}

const TIME_ZONE = 'Asia/Dubai';

const toReport = (dto: ReportSummaryDto, index: number): Report => ({
  id: dto.id,
  slNo: index + 1,
  title: dto.name,
  module: dto.module,
  template: dto.template,
  datasetId: dto.datasetId,
  timeRange: dto.timeRangeLabel,
  expireOn: dto.expireOn ?? '',
  recurrence: dto.recurrence,
  createdOn: dto.createdOn ?? '',
  type: dto.format,
  shareWith: (dto.shareWith ?? []).join(', '),
  generatedOn: dto.generatedOn ?? '',
  status: dto.status,
  error: dto.error,
  rowCount: dto.rowCount,
  truncated: dto.truncated
});

const toGeneratedReport = (dto: ReportDto): GeneratedReport => ({
  ...toReport(dto.report, 0),
  headers: dto.headers ?? [],
  rows: (dto.rows ?? []).map((row) => Object.fromEntries((dto.headers ?? []).map((header, i) => [header, row[i] ?? null])))
});

/**
 * Generated Reports from the Report Analytics API (/api/generated-reports): templates over the Data Export datasets, and reports
 * holding the rows read from those datasets when they were generated.
 */
@Injectable({ providedIn: 'root' })
export class GeneratedReportService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.reportAnalyticsApiUrl}api/generated-reports`;

  /** Recurrence of the report created last, so the list can open on its tab. */
  lastCreatedRecurrence: string | null = null;

  private fail = (err: HttpErrorResponse) => throwError(() => new Error(apiErrorMessage(err, 'Report Analytics')));

  private scope(extra: Record<string, string | null | undefined> = {}) {
    return toParams({ tenantId: environment.tenantId, clientId: environment.clientId, timeZone: TIME_ZONE, ...extra });
  }

  getTemplates(): Observable<ReportTemplateCatalog> {
    return this.http.get<ReportTemplateCatalog>(`${this.apiUrl}/templates`).pipe(catchError(this.fail));
  }

  list(recurrence?: string | null): Observable<Report[]> {
    return this.http
      .get<ReportSummaryDto[]>(this.apiUrl, { params: this.scope({ recurrence }) })
      .pipe(map((rows) => (rows ?? []).map(toReport)), catchError(this.fail));
  }

  get(id: string): Observable<GeneratedReport> {
    return this.http
      .get<ReportDto>(`${this.apiUrl}/${encodeURIComponent(id)}`, { params: this.scope() })
      .pipe(map(toGeneratedReport), catchError(this.fail));
  }

  create(form: CreateReportForm): Observable<GeneratedReport> {
    const body = { ...form, timeZone: TIME_ZONE, tenantId: environment.tenantId, clientId: environment.clientId };
    return this.http.post<ReportDto>(this.apiUrl, body).pipe(
      map((dto) => {
        this.lastCreatedRecurrence = dto.report.recurrence;
        return toGeneratedReport(dto);
      }),
      catchError(this.fail)
    );
  }

  regenerate(id: string): Observable<GeneratedReport> {
    return this.http
      .post<ReportDto>(`${this.apiUrl}/${encodeURIComponent(id)}/regenerate`, null, { params: this.scope() })
      .pipe(map(toGeneratedReport), catchError(this.fail));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`, { params: this.scope() })
      .pipe(catchError(this.fail));
  }
}

// ===== Excel export (shared by the Reports list and the report View page) =====
function reportWorksheet(report: GeneratedReport): XLSX.WorkSheet {
  if (report.headers.length > 0) {
    return XLSX.utils.json_to_sheet(report.rows, { header: report.headers });
  }
  // A failed report has no columns: export its summary row instead of an empty file.
  return XLSX.utils.json_to_sheet([{
    'Sl No': report.slNo,
    Report: report.title,
    Template: report.template,
    'Time Range': report.timeRange,
    'Expire On': report.expireOn,
    Recurrence: report.recurrence,
    'Created On': report.createdOn,
    Type: report.type,
    'Share With': report.shareWith,
    'Generated On': report.generatedOn,
    Status: report.status,
    Error: report.error ?? ''
  }]);
}

function fileSafe(name: string): string {
  return (name || 'report').replace(/[^a-z0-9]+/gi, '-').toLowerCase();
}

export function downloadReportAsExcel(report: GeneratedReport): void {
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, reportWorksheet(report), 'Report');
  XLSX.writeFile(workbook, `${fileSafe(report.title)}-${report.createdOn || new Date().toISOString().slice(0, 10)}.xlsx`);
}

export function downloadReportsAsWorkbook(reports: GeneratedReport[], fileName: string): void {
  if (reports.length === 0) return;
  const workbook = XLSX.utils.book_new();
  const used = new Set<string>();
  reports.forEach((report, index) => {
    let sheetName = fileSafe(report.title || `Report ${index + 1}`).slice(0, 28) || `report-${index + 1}`;
    // Sheet names must be unique within a workbook.
    while (used.has(sheetName)) sheetName = `${sheetName.slice(0, 25)}-${index + 1}`;
    used.add(sheetName);
    XLSX.utils.book_append_sheet(workbook, reportWorksheet(report), sheetName);
  });
  XLSX.writeFile(workbook, fileName);
}
