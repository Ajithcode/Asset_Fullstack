import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, map, of, switchMap, tap } from 'rxjs';
import { MdmApi, MdmAudit, apiErrorMessage, inSequence } from '../../shared/mdm-api';
import { EmployeeService } from '../employee/employee.service';
import { LoadOnce } from '../load-once';
import { MdmRef, PersonStatus, apiDate, apiText, nextId, stampOf } from '../people.data';
import { SkillLevel, TechnicianRecord } from './technician.model';

interface TechnicianDto extends MdmAudit {
  technicianName: string;
  technicianId: string;
  primarySkill: string | null;
  assignedTeam: MdmRef | null;
  email: string | null;
  phoneNumber: string | null;
  photo: string | null;
  skillLevel: string | null;
  secondarySkills: string[];
  certifications: { name: string; expiry: string | null }[];
  employee: MdmRef | null;
  source: string | null;
  employmentType: string | null;
  joiningDate: string | null;
  hourlyRate: number | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  address: string | null;
  availableForDispatch: boolean;
  mobileAppAccess: boolean;
  maxOpenWorkOrders: number;
}

const RESOURCE = 'technicians';

/**
 * Technicians from /api/master-data/technicians, keyed by technician ID (TECH001...); the API record id
 * of each is kept alongside (`idOf`). Each technician belongs to at most one team; the team page shows
 * them as its members.
 */
@Injectable({ providedIn: 'root' })
export class TechnicianService {
  private readonly api = inject(MdmApi);
  private readonly employees = inject(EmployeeService);
  private readonly ids = new Map<string, string>();
  private readonly loader = new LoadOnce(() => this.load());
  private records: TechnicianRecord[] = [];
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  refresh(): Observable<TechnicianRecord[]> {
    return this.loader.refresh();
  }

  loadOnce(): Observable<unknown> {
    return this.loader.once();
  }

  ensureLoaded(): void {
    this.loader.ensure();
  }

  all(): TechnicianRecord[] {
    return this.records;
  }

  find(id: string | null): TechnicianRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  idOf(id: string | null | undefined): string | undefined {
    return id ? this.ids.get(id) : undefined;
  }

  codeOfId(recordId: string | null | undefined): string {
    if (!recordId) return '';
    for (const [code, id] of this.ids) if (id === recordId) return code;
    return '';
  }

  ref(id: string | null | undefined): MdmRef | null {
    const record = this.find(id ?? null);
    const recordId = this.idOf(id);
    return record && recordId ? { id: recordId, name: record.name } : null;
  }

  nextId(): string {
    return nextId('TECH', this.records.map((r) => r.id));
  }

  isIdTaken(id: string, exceptId: string | null): boolean {
    const wanted = id.trim().toLowerCase();
    return this.records.some((r) => r.id.toLowerCase() === wanted && r.id !== exceptId);
  }

  isEmailTaken(email: string, exceptId: string | null): boolean {
    const wanted = email.trim().toLowerCase();
    return this.records.some((r) => r.email.toLowerCase() === wanted && r.id !== exceptId);
  }

  usesSkill(name: string): number {
    return this.records.filter((r) => r.primarySkill === name || r.secondarySkills.includes(name)).length;
  }

  /** Follows a skill rename the API has already applied. */
  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) =>
      r.primarySkill === from || r.secondarySkills.includes(from)
        ? { ...r, primarySkill: r.primarySkill === from ? to : r.primarySkill, secondarySkills: r.secondarySkills.map((s) => (s === from ? to : s)) }
        : r
    );
  }

  /** Creates or updates the technician with its team (`team` null leaves them out of any team). */
  persist(record: TechnicianRecord, originalId: string | null, team: MdmRef | null): Observable<TechnicianRecord> {
    const recordId = originalId ? this.ids.get(originalId) : undefined;
    const body = { ...this.toRequest(record), assignedTeam: team };
    const call = recordId ? this.api.update<TechnicianDto>(RESOURCE, recordId, body) : this.api.create<TechnicianDto>(RESOURCE, body);
    return call.pipe(
      map((dto) => {
        if (originalId) this.ids.delete(originalId);
        this.ids.set(dto.technicianId, dto.id);
        const saved = this.toRecord(dto);
        this.save(saved, recordId ? originalId : null);
        return saved;
      })
    );
  }

  /** Status change; people who are away can't be dispatched, so that is saved with it. */
  setStatus(record: TechnicianRecord, status: PersonStatus, team: MdmRef | null): Observable<TechnicianRecord> {
    const available = status === 'Active' || status === 'On Duty';
    if (record.availableForDispatch && !available) {
      return this.persist({ ...record, status, availableForDispatch: false }, record.id, team);
    }
    return this.api.setStatus<TechnicianDto>(RESOURCE, this.ids.get(record.id)!, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, record.id);
        return saved;
      })
    );
  }

  delete(ids: string[]): Observable<unknown> {
    return inSequence(
      ids.map((id) => {
        const recordId = this.ids.get(id);
        return (recordId ? this.api.remove(RESOURCE, recordId) : of(null)).pipe(
          tap(() => {
            this.ids.delete(id);
            this.records = this.records.filter((r) => r.id !== id);
          })
        );
      })
    );
  }

  private load(): Observable<TechnicianRecord[]> {
    this.loading.set(true);
    this.error.set(null);
    // Employees first, so linked employees resolve to employee IDs
    return this.employees.loadOnce().pipe(
      switchMap(() => this.api.list<TechnicianDto>(RESOURCE)),
      map((dtos) => {
        this.ids.clear();
        dtos.forEach((d) => this.ids.set(d.technicianId, d.id));
        this.records = dtos.map((d) => this.toRecord(d));
        return this.records;
      }),
      tap({ error: (err) => this.error.set(apiErrorMessage(err)) }),
      finalize(() => this.loading.set(false))
    );
  }

  private save(record: TechnicianRecord, originalId: string | null): void {
    const index = originalId ? this.records.findIndex((r) => r.id === originalId) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [record, ...this.records];
    }
  }

  private toRecord(dto: TechnicianDto): TechnicianRecord {
    return {
      id: dto.technicianId,
      name: dto.technicianName,
      photo: dto.photo,
      primarySkill: dto.primarySkill ?? '',
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      status: (dto.status as PersonStatus) || 'Active',
      skillLevel: (dto.skillLevel as SkillLevel) || 'Intermediate',
      secondarySkills: dto.secondarySkills ?? [],
      certifications: (dto.certifications ?? []).map((c) => ({ name: c.name, expiry: c.expiry ?? '' })),
      employeeId: this.employees.codeOfId(dto.employee?.id),
      source: dto.source === 'Contractor' ? 'Contractor' : 'In-house',
      employmentType: dto.employmentType ?? '',
      joiningDate: dto.joiningDate ?? '',
      hourlyRate: dto.hourlyRate,
      emergencyName: dto.emergencyContactName ?? '',
      emergencyPhone: dto.emergencyContactPhone ?? '',
      address: dto.address ?? '',
      availableForDispatch: dto.availableForDispatch,
      mobileAppAccess: dto.mobileAppAccess,
      maxOpenWorkOrders: dto.maxOpenWorkOrders,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  private toRequest(r: TechnicianRecord): object {
    return {
      technicianName: r.name,
      technicianId: r.id,
      primarySkill: apiText(r.primarySkill),
      email: apiText(r.email),
      phoneNumber: apiText(r.phone),
      photo: r.photo,
      skillLevel: r.skillLevel,
      secondarySkills: r.secondarySkills,
      certifications: r.certifications.map((c) => ({ name: c.name, expiry: apiDate(c.expiry) })),
      employee: this.employees.ref(r.employeeId),
      source: r.source,
      employmentType: apiText(r.employmentType),
      joiningDate: apiDate(r.joiningDate),
      hourlyRate: r.hourlyRate,
      emergencyContactName: apiText(r.emergencyName),
      emergencyContactPhone: apiText(r.emergencyPhone),
      address: apiText(r.address),
      availableForDispatch: r.availableForDispatch,
      mobileAppAccess: r.mobileAppAccess,
      maxOpenWorkOrders: r.maxOpenWorkOrders,
      status: r.status
    };
  }
}
