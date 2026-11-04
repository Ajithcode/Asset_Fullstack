import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, expand, map, of, reduce } from 'rxjs';
import { ASSET_API } from '../../services/asset-api';
import { AssignmentDepartmentDto, AssignmentEmployeeDto, AssignmentLocationDto, AssignmentOtherEntityDto } from '../../asset-assignment/asset-assignment.service';

// ===== Contracts (Contracts/AssetBulkAssignmentContracts.cs, camelCase) =====

/** Server field keys of the bulk assignment file. */
export type BulkFieldKey =
  | 'assetTag' | 'assetName' | 'assetType' | 'assignmentType' | 'assignTo' | 'department' | 'location' | 'startDate' | 'endDate' | 'purpose' | 'costCenter' | 'notes';

export interface BulkFieldDto {
  key: BulkFieldKey;
  label: string;
  dataType: 'Text' | 'Lookup' | 'Date';
  required: boolean;
}

export type BulkValuesDto = Record<BulkFieldKey, string | null>;

export interface BulkCountsDto {
  totalRecords: number;
  validRecords: number;
  warnings: number;
  errors: number;
  validPercent: number;
  warningPercent: number;
  errorPercent: number;
}

export interface BulkBatchDto {
  id: string;
  batchNumber: string;
  status: string | null;
  currentStep: number;
  file: { fileName: string; fileSize: number; uploadedBy: string | null; uploadedOn: string | null; totalRecords: number };
  counts: BulkCountsDto;
  columns: string[];
  mapping: Partial<Record<BulkFieldKey, string>>;
  createdBy: string | null;
  createdAt: string | null;
}

export interface BulkFileCheckDto {
  name: string;
  passed: boolean;
  message: string | null;
}

export interface BulkFileRowDto {
  rowNumber: number;
  cells: (string | null)[];
}

export interface BulkUploadDto {
  batch: BulkBatchDto;
  fileValidations: BulkFileCheckDto[];
  previewRows: BulkFileRowDto[];
  suggestedMapping: Partial<Record<BulkFieldKey, string>>;
}

export interface BulkIssueDto {
  field: string;
  severity: 'Error' | 'Warning';
  message: string;
  details: string | null;
}

export interface BulkRecordDto {
  rowNumber: number;
  status: 'Valid' | 'Warning' | 'Error' | null;
  remarks: string | null;
  assetRegistrationId: string | null;
  assetTag: string | null;
  assetName: string | null;
  assetType: string | null;
  assignmentType: string | null;
  assignTo: string | null;
  department: string | null;
  location: string | null;
  startDate: string | null;
  endDate: string | null;
  purpose: string | null;
  costCenter: string | null;
  notes: string | null;
  currentAssignmentNumber: string | null;
  isEdited: boolean;
  values: BulkValuesDto;
  issues: BulkIssueDto[];
  assignmentNumber: string | null;
  skippedReason: string | null;
}

interface BulkRecordsPageDto {
  items: BulkRecordDto[];
  totalCount: number;
  counts: BulkCountsDto;
  page: number;
  pageSize: number;
}

interface BulkFileRowsPageDto {
  columns: string[];
  items: BulkFileRowDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** SaveAssetBulkAssignmentDetailsRequest (step 4). */
export interface BulkDetailsRequest {
  assignTo: string;
  employee: AssignmentEmployeeDto | null;
  department: AssignmentDepartmentDto | null;
  otherEntity: AssignmentOtherEntityDto | null;
  location: AssignmentLocationDto | null;
  startDate: string | null;
  endDate: string | null;
  isOpenEnded: boolean;
  assignmentReason: string | null;
  reference: string | null;
  notes: string | null;
  overrideExistingAssignment: boolean;
  notifyAssignees: boolean;
  notifyDepartmentManager: boolean;
  notifyDepartment: boolean;
}

export interface BulkConfirmDto {
  batch: BulkBatchDto;
  assignedCount: number;
  skippedCount: number;
  assignments: { id: string; assignmentNumber: string; assignTo: string; assignedToName: string | null; assetCount: number }[];
  skippedRows: { rowNumber: number; assetTag: string | null; reason: string }[];
}

const PAGE = 200;

/** Bulk Assignment wizard API: /api/asset-assignments/bulk. */
@Injectable({ providedIn: 'root' })
export class BulkAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly url = `${ASSET_API}asset-assignments/bulk`;

  /** The template workbook (with sample rows unless includeSampleData is false). */
  template(includeSampleData = true): Observable<Blob> {
    return this.http.get(`${this.url}/template`, { params: { includeSampleData }, responseType: 'blob' });
  }

  fields(): Observable<BulkFieldDto[]> {
    return this.http.get<BulkFieldDto[]>(`${this.url}/fields`);
  }

  /** Step 1: stores the file's rows as a batch. */
  upload(file: File): Observable<BulkUploadDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<BulkUploadDto>(this.url, form);
  }

  /** Cancel / remove the file: deletes a batch that has not been confirmed. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }

  /** Every row as read from the file. */
  fileRows(id: string): Observable<BulkFileRowDto[]> {
    const page = (n: number) =>
      this.http
        .get<BulkFileRowsPageDto>(`${this.url}/${encodeURIComponent(id)}/file-rows`, { params: new HttpParams().set('page', n).set('pageSize', PAGE) })
        .pipe(map((r) => ({ n, r })));
    return page(1).pipe(
      expand(({ n, r }) => (n * PAGE < r.totalCount ? page(n + 1) : of())),
      reduce((all, { r }) => all.concat(r.items ?? []), [] as BulkFileRowDto[]),
    );
  }

  /** Step 2 Next: saves the mapping (field key → file column) and checks every row. */
  saveMapping(id: string, mapping: Partial<Record<BulkFieldKey, string>>): Observable<unknown> {
    return this.http.put(`${this.url}/${encodeURIComponent(id)}/mapping`, { mapping });
  }

  /** Every checked row (steps 3 and 5). */
  records(id: string): Observable<BulkRecordDto[]> {
    const page = (n: number) =>
      this.http
        .get<BulkRecordsPageDto>(`${this.url}/${encodeURIComponent(id)}/records`, { params: new HttpParams().set('page', n).set('pageSize', PAGE) })
        .pipe(map((r) => ({ n, r })));
    return page(1).pipe(
      expand(({ n, r }) => (n * PAGE < r.totalCount ? page(n + 1) : of())),
      reduce((all, { r }) => all.concat(r.items ?? []), [] as BulkRecordDto[]),
    );
  }

  /** Edit Data: replaces the values of the given rows; every row is checked again. */
  updateRecords(id: string, rows: { rowNumber: number; values: BulkValuesDto }[]): Observable<unknown> {
    return this.http.put(`${this.url}/${encodeURIComponent(id)}/records`, { rows });
  }

  /** Export Errors: rows with errors (and warnings) as an Excel file. */
  exportIssues(id: string, includeWarnings = true): Observable<Blob> {
    return this.http.get(`${this.url}/${encodeURIComponent(id)}/issues/export`, { params: { includeWarnings }, responseType: 'blob' });
  }

  /** Step 4 Next: common details for rows that leave them blank. */
  saveDetails(id: string, request: BulkDetailsRequest): Observable<BulkBatchDto> {
    return this.http.put<BulkBatchDto>(`${this.url}/${encodeURIComponent(id)}/details`, request);
  }

  /** Step 5: creates the assignments. */
  confirm(id: string, sendNotifications: boolean, includeWarnings: boolean, excludedRowNumbers: number[]): Observable<BulkConfirmDto> {
    return this.http.post<BulkConfirmDto>(`${this.url}/${encodeURIComponent(id)}/confirm`, { sendNotifications, includeWarnings, excludedRowNumbers });
  }
}

/** Saves a downloaded file. */
export function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url));
}
