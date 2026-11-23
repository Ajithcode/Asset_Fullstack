import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, of, switchMap, throwError } from 'rxjs';
import { environment } from 'shared-ui';
import { ASSET_API } from '../asset-api';

/** One column of the asset import file, as the page uses it. */
export interface ImportFieldDto {
  key: string;
  label: string;
  dataType: string;
  required: boolean;
  importable: boolean;
  /** Allowed values for lookup fields, when the API sends them. */
  allowedValues: string[];
  /** Format or help text for the column, when the API sends it. */
  description: string;
}

type RawField = Record<string, unknown>;

type MasterResource = 'locations' | 'sites' | 'departments' | 'criticalities';

/** Filters of POST exports / GET exports/count (AssetExportFilters in the API). */
export interface AssetExportFilters {
  search?: string;
  category?: string;
  assetType?: string;
  status?: string;
  locationId?: string;
  department?: string;
  criticality?: string;
  condition?: string;
  manufacturer?: string;
  /** ISO date-time. */
  nextMaintenanceFrom?: string;
  nextMaintenanceTo?: string;
}

/** Body of POST asset-import-export/exports. */
export interface AssetExportRequest {
  exportType: string;
  fieldSet: string;
  /** Server field keys; sent for a custom field set. */
  fields: string[] | null;
  filters: AssetExportFilters;
  fileFormat: string;
}

/** Result of POST asset-import-export/imports: the import session the next steps work on. */
export interface ImportUploadResult {
  /** Import session id, used by mapping / validate / confirm. */
  id: string;
  /** Raw response body, for fields the page may need later. */
  raw: RawField;
}

/** Result of POST asset-import-export/imports/{id}/confirm. Counts are null when the API does not send them. */
export interface ImportConfirmResult {
  created: number | null;
  updated: number | null;
  failed: number | null;
  message: string;
  raw: RawField;
}

/** Result of POST asset-import-export/imports/{id}/validate. Counts are null when the API does not send them. */
export interface ImportValidateResult {
  valid: number | null;
  invalid: number | null;
  duplicate: number | null;
  /** Rows the server rejected (response "problems"), with one message per issue. */
  problems: ImportRecordDto[];
  message: string;
  raw: RawField;
}

/** All plain objects in a response (body, data, summary, result ...), outermost first. */
function objectsIn(body: unknown, limit = 50): RawField[] {
  const queue: unknown[] = [body];
  const seen = new Set<unknown>();
  const out: RawField[] = [];
  while (queue.length && out.length < limit) {
    const node = queue.shift();
    if (!node || typeof node !== 'object' || Array.isArray(node) || seen.has(node)) continue;
    seen.add(node);
    out.push(node as RawField);
    for (const v of Object.values(node as RawField)) if (v && typeof v === 'object' && !Array.isArray(v)) queue.push(v);
  }
  return out;
}

/** Unwraps { data: {...} } and reads the first numeric field among the given names, at any nesting (case-insensitive). */
function readBody(body: unknown): { raw: RawField; outer: RawField; num: (...names: string[]) => number | null } {
  const outer = (body && typeof body === 'object' ? body : {}) as RawField;
  const raw = (outer['data'] && typeof outer['data'] === 'object' ? outer['data'] : outer) as RawField;
  const objects = objectsIn(body);
  const num = (...names: string[]) => {
    const wanted = names.map((n) => n.toLowerCase());
    for (const name of wanted)
      for (const o of objects)
        for (const [k, v] of Object.entries(o)) if (k.toLowerCase() === name && v !== null && v !== '' && !isNaN(Number(v))) return Number(v);
    return null;
  };
  return { raw, outer, num };
}

/** One row of GET imports/{id}/records, as the page shows it. */
export interface ImportRecordDto {
  row: number;
  tag: string;
  status: string;
  /** True when the server marked the row valid (no errors). */
  valid: boolean;
  message: string;
  /** One text per problem, e.g. "Category: Category 'HVAC' is not configured. ..." */
  issues: string[];
}

/** Error text of a record: errors may be strings, { message } objects or a { field: [msg] } map. */
function errorText(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (typeof v === 'string') return v;
  if (Array.isArray(v)) return v.map(errorText).filter(Boolean).join('; ');
  if (typeof v === 'object') {
    const o = v as RawField;
    const msg = o['message'] ?? o['errorMessage'] ?? o['error'] ?? o['description'];
    if (msg !== undefined) return [o['column'] ?? o['field'] ?? o['fieldName'], text(msg)].filter(Boolean).join(': ');
    return Object.entries(o).map(([k, x]) => `${k}: ${errorText(x)}`).join('; ');
  }
  return String(v);
}

function toRecord(raw: RawField, index: number): ImportRecordDto {
  const pick = (...names: string[]) => {
    for (const n of names) for (const [k, v] of Object.entries(raw)) if (k.toLowerCase() === n.toLowerCase() && v !== null && v !== undefined && v !== '') return v;
    return undefined;
  };
  const values = (pick('values', 'data', 'rowData', 'fields') ?? {}) as RawField;
  const errs = pick('issues', 'errors', 'validationErrors', 'errorMessages', 'messages', 'errorMessage', 'error', 'message');
  const issues = (Array.isArray(errs) ? errs.map(errorText) : [errorText(errs)]).filter(Boolean);
  const message = issues.join('; ');
  const status = text(pick('status', 'validationStatus', 'state', 'result'));
  const isValidFlag = pick('isValid', 'valid');
  const valid = isValidFlag !== undefined ? !!isValidFlag && isValidFlag !== 'false' : status ? /^(valid|ok|success|ready|passed)$/i.test(status) : !message;
  return {
    row: Number(pick('rowNumber', 'row', 'rowNo', 'lineNumber', 'excelRow', 'rowIndex') ?? index + 2),
    tag: text(pick('assetTag', 'tag', 'assetId', 'assetCode') ?? values['Asset Tag'] ?? values['assetTag'] ?? values['Asset ID'] ?? ''),
    status,
    valid,
    message,
    issues,
  };
}

const text = (v: unknown): string => (v === null || v === undefined ? '' : String(v));

/** One row of GET asset-import-export/history. */
export interface ImportExportHistoryDto {
  id: string;
  jobNumber: string;
  fileName: string;
  /** Import or Export. */
  type: string;
  records: number | null;
  status: string;
  /** ISO date-time of the job. */
  at: string;
  by: string;
}

/** One page of GET asset-import-export/history. */
export interface ImportExportHistoryPage {
  items: ImportExportHistoryDto[];
  total: number;
}

/** Reads one history row whatever naming the API uses (fileName or name, createdAt or date, ...). */
function toHistory(raw: RawField): ImportExportHistoryDto {
  const pick = (...names: string[]) => {
    for (const n of names) for (const [k, v] of Object.entries(raw)) if (k.toLowerCase() === n.toLowerCase() && v !== null && v !== undefined && v !== '') return v;
    return undefined;
  };
  const summary = (pick('summary') ?? {}) as RawField;
  const count = pick('totalRecords', 'records', 'recordCount', 'total', 'rowCount') ?? summary['total'];
  const kind = text(pick('type', 'jobType', 'operation', 'direction'));
  return {
    id: text(pick('id', 'jobId', 'importId', 'exportId')),
    jobNumber: text(pick('jobNumber', 'jobNo', 'number')),
    fileName: text(pick('fileName', 'name', 'file')),
    type: kind || (pick('exportType') !== undefined ? 'Export' : 'Import'),
    records: count === undefined || isNaN(Number(count)) ? null : Number(count),
    status: text(pick('status', 'state')),
    at: text(pick('completedAt', 'createdAt', 'date', 'timestamp', 'createdOn')),
    by: text(pick('createdBy', 'by', 'user', 'userName', 'performedBy')),
  };
}

/** Reads one field whatever casing / naming the API uses (key or name, required or isRequired, ...). */
function toField(raw: RawField): ImportFieldDto {
  const pick = (...names: string[]) => names.map((n) => raw[n]).find((v) => v !== undefined && v !== null);
  const allowed = pick('allowedValues', 'options', 'values', 'lookupValues');
  return {
    key: text(pick('key', 'fieldKey', 'name', 'field')),
    label: text(pick('label', 'displayName', 'header', 'columnName', 'name', 'key')),
    dataType: text(pick('dataType', 'type', 'fieldType')) || 'Text',
    required: !!pick('required', 'isRequired', 'mandatory', 'isMandatory'),
    importable: pick('importable', 'isImportable') !== false,
    allowedValues: Array.isArray(allowed) ? allowed.map((a) => (a && typeof a === 'object' ? text((a as RawField)['label'] ?? (a as RawField)['value']) : text(a))) : [],
    description: text(pick('description', 'format', 'hint', 'example')),
  };
}

/** Asset bulk import / export API: /api/asset-import-export. */
@Injectable({ providedIn: 'root' })
export class AssetBulkImport {
  private readonly http = inject(HttpClient);
  private readonly url = `${ASSET_API}asset-import-export`;

  /** Fields of the import file; importType narrows them (create / update / upsert) when given. */
  fields(importableOnly = false, importType?: string): Observable<ImportFieldDto[]> {
    let params = new HttpParams().set('importableOnly', importableOnly);
    if (importType) params = params.set('importType', importType);
    return this.http.get<unknown>(`${this.url}/fields`, { params }).pipe(
      map((body) => {
        const list = Array.isArray(body) ? body : ((body as RawField)?.['items'] ?? (body as RawField)?.['fields'] ?? (body as RawField)?.['data']);
        return Array.isArray(list) ? (list as RawField[]).map(toField).filter((f) => f.key) : [];
      }),
    );
  }

  /** Uploads the filled template (multipart: file, importType, template) and starts an import session. */
  upload(file: File, importType: string, template: string): Observable<ImportUploadResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('importType', importType);
    form.append('template', template);
    return this.http.post<unknown>(`${this.url}/imports`, form, { observe: 'response' }).pipe(
      map((res) => {
        const body = res.body;
        const outer = (body && typeof body === 'object' ? body : {}) as RawField;
        const raw = (outer['data'] && typeof outer['data'] === 'object' ? outer['data'] : outer) as RawField;
        // Id from the body (any nesting / naming), else the Location header (.../imports/{id}), else a plain-text body
        const fromLocation = /\/imports\/([^/?#]+)/i.exec(res.headers.get('Location') ?? '')?.[1] ?? '';
        const id = findImportId(body) || fromLocation || (typeof body === 'string' || typeof body === 'number' ? text(body).replace(/"/g, '').trim() : '');
        return { id, raw };
      }),
    );
  }

  /**
   * Names of one Master Data list for the export filters: GET {masterDataApiUrl}api/master-data/{resource}
   * (Master Data service, port 5500 — the Asset service on 5401 answers these with 404).
   * Skips inactive / deleted records; an empty list when the call fails.
   */
  masterNames(resource: MasterResource, labelField: string): Observable<string[]> {
    return this.masterOptions(resource, labelField).pipe(map((list) => list.map((o) => o.label)));
  }

  /** Same as masterNames, keeping each record id: { label, value: id }. */
  masterOptions(resource: MasterResource, labelField: string): Observable<{ label: string; value: string }[]> {
    return this.http.get<unknown>(`${environment.masterDataApiUrl}api/master-data/${resource}`).pipe(
      map((body) => {
        const list = Array.isArray(body) ? body : ((body as RawField)?.['items'] ?? (body as RawField)?.['data']);
        return (Array.isArray(list) ? (list as RawField[]) : [])
          .filter((r) => r['isDeleted'] !== true && !/^(inactive|disabled|deleted)$/i.test(text(r['status'])))
          .map((r) => ({ label: text(r[labelField] ?? r['name'] ?? r['code']), value: text(r['id'] ?? r['_id'] ?? r[labelField]) }))
          .filter((o) => o.label && o.value);
      }),
      catchError((err) => {
        console.warn(`[asset-bulk] master-data/${resource} failed`, err);
        return of([] as { label: string; value: string }[]);
      }),
    );
  }

  /** Number of assets the export would include: GET exports/count (PascalCase query names). */
  exportCount(filters: AssetExportFilters): Observable<number | null> {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(filters)) if (v) params = params.set(k.charAt(0).toUpperCase() + k.slice(1), v);
    return this.http.get<unknown>(`${this.url}/exports/count`, { params }).pipe(
      map((body) => (typeof body === 'number' ? body : readBody(body).num('count', 'total', 'totalCount', 'records', 'recordCount'))),
      catchError((err) => {
        console.warn('[asset-bulk] exports/count failed', err);
        return of(null);
      }),
    );
  }

  /**
   * Runs the export: POST exports. A file response is returned as a blob; a JSON response
   * (export job) is returned as `json`, with a download link when it holds one.
   */
  export(request: AssetExportRequest): Observable<{ blob?: Blob; fileName: string; json?: RawField; downloadUrl?: string }> {
    return this.http.post(`${this.url}/exports`, request, { responseType: 'blob', observe: 'response' }).pipe(
      switchMap((res) => {
        const blob = res.body ?? new Blob();
        const type = res.headers.get('Content-Type') ?? blob.type;
        const fallback = `Asset_Export.${request.fileFormat === 'csv' ? 'csv' : 'xlsx'}`;
        if (!/json/i.test(type)) return of({ blob, fileName: fileNameOf(res.headers.get('Content-Disposition')) || fallback });
        return from(blob.text()).pipe(
          map((txt) => {
            let json: RawField = {};
            try {
              json = JSON.parse(txt) as RawField;
            } catch {
              /* not JSON after all */
            }
            const linkObj = objectsIn(json).find((o) => ['downloadUrl', 'fileUrl', 'url', 'filePath', 'link'].some((k) => typeof o[k] === 'string'));
            const downloadUrl = linkObj ? text(['downloadUrl', 'fileUrl', 'url', 'filePath', 'link'].map((k) => linkObj[k]).find((v) => typeof v === 'string')) : undefined;
            const name = objectsIn(json).map((o) => o['fileName']).find((v) => typeof v === 'string');
            return { json, downloadUrl, fileName: text(name) || fallback };
          }),
        );
      }),
      catchError((err: unknown) => blobErrorToJson(err)),
    );
  }

  /** Import / export jobs, newest first: GET history?includeInProgress=false&page=1&pageSize=10. */
  history(page = 1, pageSize = 10, includeInProgress = false): Observable<ImportExportHistoryPage> {
    const params = new HttpParams().set('includeInProgress', includeInProgress).set('page', page).set('pageSize', pageSize);
    return this.http.get<unknown>(`${this.url}/history`, { params }).pipe(
      map((body) => {
        const obj = (body && typeof body === 'object' && !Array.isArray(body) ? body : {}) as RawField;
        const inner = (obj['data'] && typeof obj['data'] === 'object' && !Array.isArray(obj['data']) ? obj['data'] : obj) as RawField;
        const list = Array.isArray(body) ? body : ['items', 'data', 'records', 'results', 'history'].map((k) => inner[k] ?? obj[k]).find(Array.isArray);
        const items = Array.isArray(list) ? (list as RawField[]).map(toHistory) : [];
        const total = readBody(body).num('totalCount', 'total', 'totalItems', 'totalRecords', 'count') ?? items.length;
        return { items, total };
      }),
    );
  }

  /** Downloads the import template file built by the server (GET asset-import-export/template). */
  template(includeSampleData = true): Observable<{ blob: Blob; fileName: string }> {
    const params = new HttpParams().set('includeSampleData', includeSampleData);
    return this.http.get(`${this.url}/template`, { params, responseType: 'blob', observe: 'response' }).pipe(
      map((res) => ({ blob: res.body ?? new Blob(), fileName: fileNameOf(res.headers.get('Content-Disposition')) || 'Asset_Import_Template.xlsx' })),
      catchError((err: unknown) => blobErrorToJson(err)),
    );
  }

  /** Row problems of a validated import as an Excel file (GET asset-import-export/imports/{id}/errors/export). */
  exportErrors(importId: string): Observable<{ blob: Blob; fileName: string }> {
    return this.http.get(`${this.url}/imports/${encodeURIComponent(importId)}/errors/export`, { responseType: 'blob', observe: 'response' }).pipe(
      map((res) => ({ blob: res.body ?? new Blob(), fileName: fileNameOf(res.headers.get('Content-Disposition')) || 'Asset_Import_Errors.xlsx' })),
      catchError((err: unknown) => blobErrorToJson(err)),
    );
  }

  /** Discards an import that was not confirmed (DELETE asset-import-export/imports/{id}). */
  deleteImport(importId: string): Observable<unknown> {
    return this.http.delete(`${this.url}/imports/${encodeURIComponent(importId)}`);
  }

  /** Saves the column mapping: PUT asset-import-export/imports/{id}/mapping, body { importType, mapping: { fileColumn: fieldKey | null } }. */
  saveMapping(importId: string, importType: string, mapping: Record<string, string | null>): Observable<unknown> {
    return this.http.put<unknown>(`${this.url}/imports/${encodeURIComponent(importId)}/mapping`, { importType, mapping });
  }

  /** Validates the rows on the server: POST asset-import-export/imports/{id}/validate. */
  validate(importId: string): Observable<ImportValidateResult> {
    return this.http.post<unknown>(`${this.url}/imports/${encodeURIComponent(importId)}/validate`, {}).pipe(
      map((body) => {
        const { raw, outer, num } = readBody(body);
        const holder = objectsIn(body).find((o) => ['problems', 'invalidRows', 'rowErrors'].some((k) => Array.isArray(o[k])));
        const list = holder ? (['problems', 'invalidRows', 'rowErrors'].map((k) => holder[k]).find(Array.isArray) as RawField[]) : [];
        return {
          duplicate: num('duplicateCount', 'duplicate', 'duplicates'),
          problems: list.map(toRecord),
          valid: num('validCount', 'validRows', 'validRecords', 'valid', 'validRowCount', 'successCount', 'readyCount'),
          invalid: num('invalidCount', 'invalidRows', 'invalidRecords', 'invalid', 'invalidRowCount', 'errorCount', 'errorRows', 'failedCount', 'errors'),
          message: text(raw['message'] ?? outer['message'] ?? (typeof body === 'string' ? body : '')),
          raw,
        };
      }),
    );
  }

  /** Rows of the import with their server validation result: GET imports/{id}/records. */
  records(importId: string, pageSize = 500): Observable<ImportRecordDto[]> {
    const params = new HttpParams().set('page', 1).set('pageSize', pageSize);
    return this.http.get<unknown>(`${this.url}/imports/${encodeURIComponent(importId)}/records`, { params }).pipe(
      map((body) => {
        const holder = objectsIn(body).find((o) => ['items', 'records', 'rows', 'data', 'results'].some((k) => Array.isArray(o[k])));
        const list = Array.isArray(body) ? body : holder ? ['items', 'records', 'rows', 'data', 'results'].map((k) => holder[k]).find(Array.isArray) : [];
        return ((list ?? []) as RawField[]).map(toRecord);
      }),
    );
  }

  /** Commits the uploaded import session: POST asset-import-export/imports/{id}/confirm. */
  confirm(importId: string): Observable<ImportConfirmResult> {
    return this.http.post<unknown>(`${this.url}/imports/${encodeURIComponent(importId)}/confirm`, {}).pipe(
      map((body) => {
        const outer = (body && typeof body === 'object' ? body : {}) as RawField;
        const raw = (outer['data'] && typeof outer['data'] === 'object' ? outer['data'] : outer) as RawField;
        const num = (...names: string[]) => {
          const v = names.map((n) => raw[n]).find((x) => x !== undefined && x !== null);
          return v === undefined || v === null || isNaN(Number(v)) ? null : Number(v);
        };
        return {
          created: num('created', 'createdCount', 'createdRows', 'inserted', 'insertedCount'),
          updated: num('updated', 'updatedCount', 'updatedRows'),
          failed: num('failed', 'failedCount', 'failedRows', 'errorCount', 'skipped', 'skippedCount'),
          message: text(raw['message'] ?? outer['message'] ?? (typeof body === 'string' ? body : '')),
          raw,
        };
      }),
    );
  }
}

/** Keys that hold the import session id, best first. */
const ID_KEYS = ['importId', 'importSessionId', 'importJobId', 'importBatchId', 'sessionId', 'jobId', 'batchId', 'id', 'Id', 'ID', 'importID'];
/** *Id keys that are never the import id. */
const NOT_ID = /^(tenant|client|user|created|updated|modified|owner|org|organization|site|template|file|asset|category|type)/i;

/** Finds the import id in a response, whatever its nesting (data / result / value / import ...) or key name. */
function findImportId(body: unknown): string {
  const queue: unknown[] = [body];
  const seen = new Set<unknown>();
  const objects: RawField[] = [];
  while (queue.length && objects.length < 50) {
    const node = queue.shift();
    if (!node || typeof node !== 'object' || seen.has(node) || Array.isArray(node)) continue;
    seen.add(node);
    objects.push(node as RawField);
    for (const v of Object.values(node as RawField)) if (v && typeof v === 'object' && !Array.isArray(v)) queue.push(v);
  }
  const ok = (v: unknown) => (typeof v === 'string' && v.trim()) || typeof v === 'number';
  for (const key of ID_KEYS) for (const o of objects) if (ok(o[key])) return text(o[key]);
  // Last resort: any other "...Id" key that is not tenant / user / ...
  for (const o of objects) for (const [k, v] of Object.entries(o)) if (/id$/i.test(k) && !NOT_ID.test(k) && ok(v)) return text(v);
  return '';
}

/** File name from a Content-Disposition header (filename* or filename). */
function fileNameOf(header: string | null): string {
  if (!header) return '';
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) return decodeURIComponent(star[1].trim().replace(/"/g, ''));
  return /filename="?([^";]+)"?/i.exec(header)?.[1]?.trim() ?? '';
}

/** Blob requests return their error body as a Blob; turn it back into JSON so apiErrorMessage can read it. */
function blobErrorToJson(err: unknown): Observable<never> {
  if (!(err instanceof HttpErrorResponse) || !(err.error instanceof Blob)) return throwError(() => err);
  return from(err.error.text()).pipe(
    switchMap((body) => {
      let parsed: unknown = body;
      try {
        parsed = JSON.parse(body);
      } catch {
        /* plain text body */
      }
      return throwError(() => new HttpErrorResponse({ error: parsed, headers: err.headers, status: err.status, statusText: err.statusText, url: err.url ?? undefined }));
    }),
  );
}
