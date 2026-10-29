import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { DepartmentService } from '../../organization/department/department.service';
import { nextId } from '../people.data';
import { blankEmployee } from './employee.data';
import { EmployeeRecord, EmployeeStatus } from './employee.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/employees */
interface EmployeeDto {
  id: string;
  employeeName: string;
  // The organisation's employee number, e.g. EMP001
  employeeId: string;
  department: ReferenceDto | null;
  designation: string | null;
  email: string | null;
  phoneNumber: string | null;
  reportingManager: string | null;
  skills: string[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Employees from the Master Data API. Rows are cached after load() so the People pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed. The
 * department is shown by name and linked by id. Employment, contact, certification and access
 * details are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly departments = inject(DepartmentService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/employees`;

  private records: EmployeeRecord[] = [];
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by id can map them. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/employees (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<EmployeeDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.records = list.map((d) => this.toRecord(d));
        this.loading = false;
        this.settled.next();
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
        this.settled.next();
      }
    });
  }

  all(): EmployeeRecord[] {
    return this.records;
  }

  find(id: string | null): EmployeeRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  nextId(): string {
    return nextId('EMP', this.records.map((r) => r.id));
  }

  isIdTaken(id: string, exceptId: string | null): boolean {
    const wanted = id.trim().toLowerCase();
    return this.records.some((r) => r.id.toLowerCase() === wanted && r.id !== exceptId);
  }

  isEmailTaken(email: string, exceptId: string | null): boolean {
    const wanted = email.trim().toLowerCase();
    return this.records.some((r) => r.email.toLowerCase() === wanted && r.id !== exceptId);
  }

  /** Employees who report to `id`. */
  reportsOf(id: string): EmployeeRecord[] {
    return this.records.filter((r) => r.reportingManager === id);
  }

  usesSkill(name: string): number {
    return this.records.filter((r) => r.skills.includes(name)).length;
  }

  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.skills.includes(from) ? { ...r, skills: r.skills.map((s) => (s === from ? to : s)) } : r));
  }

  /** POST for a new record, PUT /{recordId} for an existing one. */
  save(record: EmployeeRecord, originalId: string | null): Observable<EmployeeRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const department = this.departments.all().find((d) => d.name === record.department);
    const body = {
      employeeName: record.name,
      employeeId: record.id,
      department: department ? { id: department.id, name: department.name } : null,
      designation: record.designation,
      email: record.email,
      phoneNumber: record.phone,
      reportingManager: record.reportingManager || null,
      skills: record.skills,
      status: record.status
    };
    const request$ = record.recordId
      ? this.http.put<EmployeeDto>(`${this.apiUrl}/${record.recordId}`, { ...body, updatedBy: user })
      : this.http.post<EmployeeDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.recordId === saved.recordId);
        const list = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
        // Reports follow a changed manager ID (the next save of each report stores it)
        this.records = originalId && originalId !== saved.id ? list.map((r) => (r.reportingManager === originalId ? { ...r, reportingManager: saved.id } : r)) : list;
      })
    );
  }

  /** DELETE /api/master-data/employees/{recordId}; the API refuses department heads and warehouse managers. */
  remove(record: EmployeeRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.recordId}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.recordId !== record.recordId))));
  }

  private toRecord(dto: EmployeeDto): EmployeeRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.recordId === dto.id) ?? blankEmployee(dto.employeeId);
    return {
      ...cached,
      recordId: dto.id,
      id: dto.employeeId,
      name: dto.employeeName,
      department: dto.department?.name ?? '',
      designation: dto.designation ?? '',
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      reportingManager: dto.reportingManager ?? '',
      skills: dto.skills,
      status: (['Active', 'On Leave', 'Inactive'].includes(dto.status ?? '') ? dto.status : 'Active') as EmployeeStatus,
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
