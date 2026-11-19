import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ASSET_API } from '../services/asset-api';

/**
 * Assets › Bulk Import / Export, backed by the Asset service: /api/asset-import-export
 * (see AssetImportExportEndpoints in the backend).
 *
 * Import: template → upload (POST /imports) → mapping (PUT /imports/{id}/mapping, POST …/mapping/auto)
 * → validate (POST /imports/{id}/validate) → confirm (POST /imports/{id}/confirm).
 * Export: POST /exports (the file), GET /exports/count. Both are listed by GET /history.
 */
const API = `${ASSET_API}asset-import-export`;

export type BulkImportType = 'Create' | 'Update' | 'Upsert';
export type BulkExportType = 'AssetMasterData' | 'AssetWithRelatedData' | 'CustomExport';
export type BulkFieldSet = 'Standard' | 'All' | 'Custom';
export type BulkFileFormat = 'xlsx' | 'csv';

export interface BulkOption {
  value: string;
  label: string;
}

export interface BulkTemplate {
  key: string;
  name: string;
  description: string;
  importType: BulkImportType;
}

export interface BulkOptions {
  importTypes: BulkOption[];
  templates: BulkTemplate[];
  exportTypes: BulkOption[];
  fieldSets: BulkOption[];
  fileFormats: BulkOption[];
  maxImportRows: number;
  maxExportRows: number;
  maxFileSizeBytes: number;
}

/** An Asset360 field a column can be mapped to (and that can be exported). Custom fields have keys "custom:<name>". */
export interface BulkField {
  key: string;
  label: string;
  group: string;
  dataType: string;
  mandatory: boolean;
  importable: boolean;
  standard: boolean;
  hint?: string | null;
  options?: string[] | null;
  format?: string | null;
  example?: string | null;
}

export interface BulkSummary {
  total: number;
  valid: number;
  invalid: number;
  duplicate: number;
  toCreate: number;
  toUpdate: number;
  created: number;
  updated: number;
  failed: number;
  validatedAt?: string | null;
}

export interface BulkJob {
  id: string;
  jobNumber: string;
  fileName: string;
  fileSize: number;
  fileFormat: string;
  importType: BulkImportType;
  template?: string | null;
  currentStep: number;
  status?: string | null;
  totalRecords: number;
  columnCount: number;
  emptyRowCount: number;
  summary?: BulkSummary | null;
  createdBy?: string | null;
  createdAt?: string | null;
  completedAt?: string | null;
}

export interface BulkColumn {
  column: string;
  mappedTo?: string | null;
  mappedToLabel?: string | null;
  dataType?: string | null;
  mandatory: boolean;
  sampleData?: string | null;
  format?: string | null;
  example?: string | null;
  status: string;
}

export interface BulkMapping {
  import: BulkJob;
  columns: BulkColumn[];
  mappedCount: number;
  /** Labels of mandatory fields no column is mapped to yet. */
  missingMandatory: string[];
  canValidate: boolean;
  fields: BulkField[];
}

export interface BulkFileCheck {
  name: string;
  passed: boolean;
  message?: string | null;
}

export interface BulkUpload {
  import: BulkJob;
  fileChecks: BulkFileCheck[];
  mapping: BulkMapping;
}

export interface BulkIssue {
  field?: string | null;
  column?: string | null;
  message: string;
}

export interface BulkRecord {
  rowNumber: number;
  status?: string | null;
  action?: string | null;
  assetRegistrationId?: string | null;
  assetName?: string | null;
  assetTag?: string | null;
  values: Record<string, string | null>;
  issues: BulkIssue[];
}

export interface BulkValidation {
  import: BulkJob;
  summary: BulkSummary;
  /** The first valid records (Import Preview). */
  preview: BulkRecord[];
  /** The first invalid / duplicate records. */
  problems: BulkRecord[];
}

export interface BulkConfirm {
  import: BulkJob;
  summary: BulkSummary;
  failed: BulkRecord[];
}

export interface BulkExportFilters {
  search?: string;
  category?: string;
  status?: string;
  locationId?: string;
  department?: string;
  criticality?: string;
  nextMaintenanceFrom?: string;
  nextMaintenanceTo?: string;
}

export interface BulkExportRequest {
  exportType: BulkExportType;
  fieldSet?: BulkFieldSet;
  fields?: string[];
  filters: BulkExportFilters;
  fileFormat: BulkFileFormat;
}

export interface BulkHistoryItem {
  id: string;
  jobNumber: string;
  fileName: string;
  type: string;
  records: number;
  status?: string | null;
  createdAt?: string | null;
  createdBy?: string | null;
  completedAt?: string | null;
  importType?: string | null;
  exportType?: string | null;
  summary?: BulkSummary | null;
}

export interface BulkPage<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** A file sent by the API, with the name from its Content-Disposition header. */
export interface BulkFile {
  blob: Blob;
  fileName: string;
}

@Injectable({ providedIn: 'root' })
export class AssetBulkApi {
  private readonly http = inject(HttpClient);

  options(): Observable<BulkOptions> {
    return this.http.get<BulkOptions>(`${API}/options`);
  }

  fields(importType: BulkImportType = 'Create'): Observable<BulkField[]> {
    return this.http.get<BulkField[]>(`${API}/fields`, { params: { importType } });
  }

  template(template: string, fileFormat: BulkFileFormat = 'xlsx'): Observable<BulkFile> {
    return this.file(this.http.get(`${API}/template`, { params: { template, fileFormat }, observe: 'response', responseType: 'blob' }), `Assets_Import_Template.${fileFormat}`);
  }

  upload(file: File, importType: BulkImportType, template?: string): Observable<BulkUpload> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('importType', importType);
    if (template) form.append('template', template);
    return this.http.post<BulkUpload>(`${API}/imports`, form);
  }

  deleteImport(id: string): Observable<unknown> {
    return this.http.delete(`${API}/imports/${encodeURIComponent(id)}`);
  }

  saveMapping(id: string, mapping: Record<string, string | null>, importType?: BulkImportType): Observable<BulkMapping> {
    return this.http.put<BulkMapping>(`${API}/imports/${encodeURIComponent(id)}/mapping`, { importType, mapping });
  }

  autoMap(id: string): Observable<BulkMapping> {
    return this.http.post<BulkMapping>(`${API}/imports/${encodeURIComponent(id)}/mapping/auto`, {});
  }

  validate(id: string): Observable<BulkValidation> {
    return this.http.post<BulkValidation>(`${API}/imports/${encodeURIComponent(id)}/validate`, {});
  }

  problemRows(id: string): Observable<BulkFile> {
    return this.file(this.http.get(`${API}/imports/${encodeURIComponent(id)}/errors/export`, { observe: 'response', responseType: 'blob' }), 'Asset_Import_Errors.xlsx');
  }

  confirm(id: string): Observable<BulkConfirm> {
    return this.http.post<BulkConfirm>(`${API}/imports/${encodeURIComponent(id)}/confirm`, {});
  }

  exportCount(filters: BulkExportFilters): Observable<number> {
    return this.http.get<{ count: number }>(`${API}/exports/count`, { params: this.params(filters) }).pipe(map((r) => r.count));
  }

  export(request: BulkExportRequest): Observable<BulkFile> {
    return this.file(this.http.post(`${API}/exports`, request, { observe: 'response', responseType: 'blob' }), `Asset_Export_${new Date().toISOString().slice(0, 10)}.${request.fileFormat}`);
  }

  history(page = 1, pageSize = 50): Observable<BulkPage<BulkHistoryItem>> {
    return this.http.get<BulkPage<BulkHistoryItem>>(`${API}/history`, { params: { page, pageSize } });
  }

  /** Saves a downloaded file in the browser. */
  static save(file: BulkFile): void {
    const url = URL.createObjectURL(file.blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = file.fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  private file(request: Observable<HttpResponse<Blob>>, fallbackName: string): Observable<BulkFile> {
    return request.pipe(map((res) => ({ blob: res.body ?? new Blob(), fileName: fileNameOf(res.headers.get('content-disposition')) || fallbackName })));
  }

  private params(filters: BulkExportFilters): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return params;
  }
}

/** File name from a Content-Disposition header (filename*= or filename=). */
function fileNameOf(header: string | null): string {
  if (!header) return '';
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : '';
}
