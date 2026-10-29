import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap, take, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { BusinessUnitService } from '../business-unit/business-unit.service';
import { blankDepartment } from './department.data';
import { DepartmentFilters, DepartmentRecord, DepartmentType } from './department.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/departments */
interface DepartmentDto {
  id: string;
  departmentName: string;
  code: string;
  type: string | null;
  description: string | null;
  parentDepartment: ReferenceDto | null;
  businessUnit: ReferenceDto | null;
  departmentHead: ReferenceDto | null;
  employees: ReferenceDto[];
  assets: ReferenceDto[];
  employeeCount: number;
  assetCount: number;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  status: string | null;
  isDeleted: boolean;
}

/**
 * Fields the API stores; deputy, cost centre, contact, budget and settings are UI-only for now.
 * Employees are left out so the API keeps the current members.
 */
interface DepartmentRequest {
  departmentName: string;
  code: string;
  type: string;
  description: string;
  parentDepartment: ReferenceDto | null;
  businessUnit: ReferenceDto | null;
  departmentHead: ReferenceDto | null;
  assets: ReferenceDto[];
  status: string;
}

/**
 * Departments from the Master Data API. Rows are cached after load() so this page and the
 * Business Unit / Site pages can keep reading them synchronously; writes go to the API and update
 * the cache when they succeed.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly businessUnits = inject(BusinessUnitService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/departments`;

  private records: DepartmentRecord[] = [];
  // Raw API rows by id: head id and linked assets, which a PUT clears if it leaves them out
  private dtos = new Map<string, DepartmentDto>();
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/departments (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    // Wait for the business units so each department's unit id can be mapped to its code
    this.businessUnits.settled$.pipe(
      take(1),
      switchMap(() => this.http.get<DepartmentDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }))
    ).subscribe({
      next: (list) => {
        this.dtos = new Map(list.map((d) => [d.id, d]));
        this.records = list.map((d) => this.toRecord(d));
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): DepartmentRecord[] {
    return this.records;
  }

  find(code: string | null): DepartmentRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  ofBusinessUnit(businessUnitCode: string): DepartmentRecord[] {
    return this.records.filter((r) => r.businessUnitCode === businessUnitCode);
  }

  /** Department names for a business unit (by its name or code), for pickers on other masters. */
  names(businessUnitCode = ''): string[] {
    return this.records.filter((r) => !businessUnitCode || r.businessUnitCode === businessUnitCode).map((r) => r.name);
  }

  children(code: string): DepartmentRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  ancestry(code: string | null): DepartmentRecord[] {
    const chain: DepartmentRecord[] = [];
    for (let r = this.find(code); r && chain.length < 20; r = this.find(r.parentCode)) chain.unshift(r);
    return chain;
  }

  /** `code` and every department under it, so none can become its own parent. */
  descendantCodes(code: string): Set<string> {
    const found = new Set([code]);
    for (let added = true; added; ) {
      added = false;
      for (const r of this.records) {
        if (r.parentCode && found.has(r.parentCode) && !found.has(r.code)) {
          found.add(r.code);
          added = true;
        }
      }
    }
    return found;
  }

  filter(f: DepartmentFilters, businessUnitName: (code: string) => string): DepartmentRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.businessUnitCode || r.businessUnitCode === f.businessUnitCode) &&
        (!f.status || r.status === f.status) &&
        (!term || [r.name, r.code, r.description, businessUnitName(r.businessUnitCode)].some((v) => v.toLowerCase().includes(term)))
    );
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** Keeps departments attached when their business unit's code changes (the API links by id). */
  renameBusinessUnit(oldCode: string, newCode: string): void {
    if (oldCode === newCode) return;
    this.records = this.records.map((r) => (r.businessUnitCode === oldCode ? { ...r, businessUnitCode: newCode } : r));
  }

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: DepartmentRecord, originalCode: string | null): Observable<DepartmentRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<DepartmentDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<DepartmentDto>(this.apiUrl, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      tap((dto) => this.dtos.set(dto.id, dto)),
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto), icon: record.icon })),
      tap((saved) => {
        const rows = [...this.records];
        const index = rows.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          rows[index] = saved;
        } else {
          rows.push(saved);
        }
        // Sub-departments point at their parent by code, so follow a code change
        if (originalCode && originalCode !== saved.code) {
          rows.forEach((r, i) => {
            if (r.parentCode === originalCode) rows[i] = { ...r, parentCode: saved.code };
          });
        }
        this.records = rows;
      })
    );
  }

  /** DELETE /api/master-data/departments/{id}; the API makes its sub-departments top-level. */
  remove(record: DepartmentRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.dtos.delete(record.id);
        this.records = this.records
          .filter((r) => r.id !== record.id)
          .map((r) => (r.parentCode === record.code ? { ...r, parentCode: null } : r));
      })
    );
  }

  private toRecord(dto: DepartmentDto): DepartmentRecord {
    const unitId = dto.businessUnit?.id;
    const unit = unitId ? this.businessUnits.all().find((b) => b.id === unitId) : undefined;
    const parentId = dto.parentDepartment?.id;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankDepartment();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.departmentName,
      businessUnitCode: unit?.code ?? '',
      parentCode: parentId ? (this.dtos.get(parentId)?.code ?? null) : null,
      type: (dto.type ?? '') as DepartmentType,
      head: dto.departmentHead?.name ?? '',
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      employees: dto.employeeCount,
      assets: dto.assetCount,
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private toRequest(r: DepartmentRecord): DepartmentRequest {
    const unit = this.businessUnits.find(r.businessUnitCode);
    const parent = this.find(r.parentCode);
    const existing = r.id ? this.dtos.get(r.id) : undefined;
    const head = r.head.trim();
    // Keep the employee link when the head is unchanged; a newly typed name is stored as typed
    const departmentHead = !head ? null : existing?.departmentHead?.name === head ? existing.departmentHead : { id: null, name: head };
    return {
      departmentName: r.name,
      code: r.code,
      type: r.type,
      description: r.description,
      parentDepartment: parent ? { id: parent.id, name: parent.name } : null,
      businessUnit: unit ? { id: unit.id, name: unit.name } : null,
      departmentHead,
      assets: existing?.assets ?? [],
      status: r.status
    };
  }
}
