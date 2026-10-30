import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, map, of, tap } from 'rxjs';
import { MdmApi, MdmAudit, apiErrorMessage, inSequence } from '../../shared/mdm-api';
import { LoadOnce } from '../load-once';
import { MasterLookups } from '../name-lookup';
import { MdmRef, apiDate, apiText, nextId, stampOf } from '../people.data';
import { EmployeeRecord, EmployeeStatus } from './employee.model';

interface EmployeeDto extends MdmAudit {
  employeeName: string;
  employeeId: string;
  department: MdmRef | null;
  designation: string | null;
  email: string | null;
  phoneNumber: string | null;
  photo: string | null;
  employmentType: string | null;
  joiningDate: string | null;
  site: string | null;
  reportingManager: MdmRef | null;
  costCenter: string | null;
  alternatePhone: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  address: string | null;
  nationality: string | null;
  skills: string[];
  certifications: { name: string; expiry: string | null }[];
  accessRole: string | null;
  canApprove: boolean;
  notifyEmail: boolean;
  notifySms: boolean;
}

const RESOURCE = 'employees';

/**
 * Employees from /api/master-data/employees. Records are keyed by the employee ID (EMP001...); the API
 * record id of each is kept alongside (`idOf`). Teams list employees; the reporting manager is another employee.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly api = inject(MdmApi);
  private readonly lookups = inject(MasterLookups);
  private readonly ids = new Map<string, string>();
  private readonly loader = new LoadOnce(() => this.load());
  private records: EmployeeRecord[] = [];
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Reloads the list from the API. */
  refresh(): Observable<EmployeeRecord[]> {
    return this.loader.refresh();
  }

  /** Completes once the list has been loaded (at most one request). */
  loadOnce(): Observable<unknown> {
    return this.loader.once();
  }

  /** Loads the list once, for pages and pickers that only read it. */
  ensureLoaded(): void {
    this.loader.ensure();
  }

  /** All employees; the first read starts loading them, so pickers on other pages fill in. */
  all(): EmployeeRecord[] {
    this.loadOnFirstRead();
    return this.records;
  }

  find(id: string | null): EmployeeRecord | undefined {
    this.loadOnFirstRead();
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  private autoLoad = true;

  // Deferred: reads happen while templates render, where the loading signal can't be written
  private loadOnFirstRead(): void {
    if (!this.autoLoad) return;
    this.autoLoad = false;
    queueMicrotask(() => this.ensureLoaded());
  }

  /** API record id of the employee with this employee ID. */
  idOf(id: string | null | undefined): string | undefined {
    return id ? this.ids.get(id) : undefined;
  }

  /** Employee ID (EMP...) of the API record id. */
  codeOfId(recordId: string | null | undefined): string {
    if (!recordId) return '';
    for (const [code, id] of this.ids) if (id === recordId) return code;
    return '';
  }

  /** `{ id, name }` link the API takes for the employee, or null. */
  ref(id: string | null | undefined): MdmRef | null {
    const record = this.find(id ?? null);
    const recordId = this.idOf(id);
    return record && recordId ? { id: recordId, name: record.name } : null;
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

  /** Follows a skill rename the API has already applied. */
  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.skills.includes(from) ? { ...r, skills: r.skills.map((s) => (s === from ? to : s)) } : r));
  }

  /** Creates (no `originalId`) or updates the employee, then keeps what the API returned. */
  persist(record: EmployeeRecord, originalId: string | null): Observable<EmployeeRecord> {
    const recordId = originalId ? this.ids.get(originalId) : undefined;
    const body = this.toRequest(record);
    const call = recordId ? this.api.update<EmployeeDto>(RESOURCE, recordId, body) : this.api.create<EmployeeDto>(RESOURCE, body);
    return call.pipe(
      map((dto) => {
        if (originalId) this.ids.delete(originalId);
        this.ids.set(dto.employeeId, dto.id);
        const saved = this.toRecord(dto);
        this.save(saved, recordId ? originalId : null);
        return saved;
      })
    );
  }

  setStatus(record: EmployeeRecord, status: EmployeeStatus): Observable<EmployeeRecord> {
    return this.api.setStatus<EmployeeDto>(RESOURCE, this.ids.get(record.id)!, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, record.id);
        return saved;
      })
    );
  }

  /** Deletes one at a time; employees deleted before a failure are still removed locally. */
  delete(ids: string[]): Observable<unknown> {
    return inSequence(
      ids.map((id) => {
        const recordId = this.ids.get(id);
        return (recordId ? this.api.remove(RESOURCE, recordId) : of(null)).pipe(
          tap(() => {
            this.ids.delete(id);
            this.remove([id]);
          })
        );
      })
    );
  }

  private load(): Observable<EmployeeRecord[]> {
    this.lookups.departments.ensureLoaded();
    this.loading.set(true);
    this.error.set(null);
    return this.api.list<EmployeeDto>(RESOURCE).pipe(
      map((dtos) => {
        // Ids first, so reporting managers resolve to employee IDs
        this.ids.clear();
        dtos.forEach((d) => this.ids.set(d.employeeId, d.id));
        this.records = dtos.map((d) => this.toRecord(d));
        return this.records;
      }),
      tap({ error: (err) => this.error.set(apiErrorMessage(err)) }),
      finalize(() => this.loading.set(false))
    );
  }

  private save(record: EmployeeRecord, originalId: string | null): void {
    const index = originalId ? this.records.findIndex((r) => r.id === originalId) : -1;
    if (index >= 0) {
      const list = [...this.records];
      list[index] = record;
      // Reports follow a changed manager ID
      this.records = originalId !== record.id ? list.map((r) => (r.reportingManager === originalId ? { ...r, reportingManager: record.id } : r)) : list;
    } else {
      this.records = [record, ...this.records];
    }
  }

  private remove(ids: string[]): void {
    this.records = this.records.filter((r) => !ids.includes(r.id));
  }

  private toRecord(dto: EmployeeDto): EmployeeRecord {
    return {
      id: dto.employeeId,
      name: dto.employeeName,
      photo: dto.photo,
      department: dto.department?.name ?? '',
      designation: dto.designation ?? '',
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      status: (dto.status as EmployeeStatus) || 'Active',
      employmentType: dto.employmentType ?? '',
      joiningDate: dto.joiningDate ?? '',
      site: dto.site ?? '',
      reportingManager: this.codeOfId(dto.reportingManager?.id),
      costCenter: dto.costCenter ?? '',
      alternatePhone: dto.alternatePhone ?? '',
      emergencyName: dto.emergencyContactName ?? '',
      emergencyPhone: dto.emergencyContactPhone ?? '',
      address: dto.address ?? '',
      nationality: dto.nationality ?? '',
      skills: dto.skills ?? [],
      certifications: (dto.certifications ?? []).map((c) => ({ name: c.name, expiry: c.expiry ?? '' })),
      accessRole: dto.accessRole ?? '',
      canApprove: dto.canApprove,
      notifyEmail: dto.notifyEmail,
      notifySms: dto.notifySms,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  private toRequest(r: EmployeeRecord): object {
    return {
      employeeName: r.name,
      employeeId: r.id,
      department: this.lookups.departments.ref(r.department),
      designation: apiText(r.designation),
      email: apiText(r.email),
      phoneNumber: apiText(r.phone),
      photo: r.photo,
      employmentType: apiText(r.employmentType),
      joiningDate: apiDate(r.joiningDate),
      site: apiText(r.site),
      reportingManager: this.ref(r.reportingManager),
      costCenter: apiText(r.costCenter),
      alternatePhone: apiText(r.alternatePhone),
      emergencyContactName: apiText(r.emergencyName),
      emergencyContactPhone: apiText(r.emergencyPhone),
      address: apiText(r.address),
      nationality: apiText(r.nationality),
      skills: r.skills,
      certifications: r.certifications.map((c) => ({ name: c.name, expiry: apiDate(c.expiry) })),
      accessRole: apiText(r.accessRole),
      canApprove: r.canApprove,
      notifyEmail: r.notifyEmail,
      notifySms: r.notifySms,
      status: r.status
    };
  }
}
