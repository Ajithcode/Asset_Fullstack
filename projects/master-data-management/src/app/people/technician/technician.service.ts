import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { PersonStatus, nextId } from '../people.data';
import { blankTechnician } from './technician.data';
import { TechnicianRecord } from './technician.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/technicians */
interface TechnicianDto {
  id: string;
  technicianName: string;
  // The organisation's technician number, e.g. TECH001
  technicianId: string;
  primarySkill: string | null;
  secondarySkills: string[];
  assignedTeam: ReferenceDto | null;
  email: string | null;
  phoneNumber: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Technicians from the Master Data API. Rows are cached after load() so the People pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed. A
 * technician's team is stored on the technician (one team each); the Team service mirrors it as
 * the team's member list. Skill level, certification, employment and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class TechnicianService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/technicians`;

  private records: TechnicianRecord[] = [];
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by id can map them. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/technicians (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<TechnicianDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
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

  all(): TechnicianRecord[] {
    return this.records;
  }

  find(id: string | null): TechnicianRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
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

  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) =>
      r.primarySkill === from || r.secondarySkills.includes(from)
        ? { ...r, primarySkill: r.primarySkill === from ? to : r.primarySkill, secondarySkills: r.secondarySkills.map((s) => (s === from ? to : s)) }
        : r
    );
  }

  /** POST for a new record, PUT /{recordId} for an existing one, with the team it belongs to. */
  save(record: TechnicianRecord, team: ReferenceDto | null): Observable<TechnicianRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = {
      technicianName: record.name,
      technicianId: record.id,
      primarySkill: record.primarySkill,
      secondarySkills: record.secondarySkills,
      assignedTeam: team,
      email: record.email,
      phoneNumber: record.phone,
      status: record.status
    };
    const request$ = record.recordId
      ? this.http.put<TechnicianDto>(`${this.apiUrl}/${record.recordId}`, { ...body, updatedBy: user })
      : this.http.post<TechnicianDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.recordId === saved.recordId);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
      })
    );
  }

  /** DELETE /api/master-data/technicians/{recordId}; the API refuses team leaders. */
  remove(record: TechnicianRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.recordId}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.recordId !== record.recordId))));
  }

  private toRecord(dto: TechnicianDto): TechnicianRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.recordId === dto.id) ?? blankTechnician(dto.technicianId);
    return {
      ...cached,
      recordId: dto.id,
      id: dto.technicianId,
      name: dto.technicianName,
      primarySkill: dto.primarySkill ?? '',
      secondarySkills: dto.secondarySkills,
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      status: (['Active', 'On Duty', 'On Leave', 'Inactive'].includes(dto.status ?? '') ? dto.status : 'Active') as PersonStatus,
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
